using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using ICSharpCode.AvalonEdit;
using Semojum.Core;
using Semojum.Core.Integration;
using Semojum.Desktop.Services;

namespace Semojum.Desktop;

public partial class MainWindow
{
    private long previewGeneration;
    private CancellationTokenSource? debounce;
    private bool pendingSourceTranslation;
    private (int Line, string Text, bool Reverse) currentPreview = (0, "", false);

    private (int Line, string Text, bool Reverse)? FocusedPreviewLine()
    {
        var pane = detachedPane?.IsKeyboardFocusWithin == true ? detachedPane : Braille;
        if (pane.GridView.IsKeyboardFocusWithin)
        {
            var grid = pane.GridView;
            if (!grid.CoordinatesCurrent || grid.CaretCoordinates is not { } at) return (0, "", true);
            var face = (BrailleGridFace)grid.Items[at.Face - 1];
            var row = face.Text is { } printed ? printed[at.Line - 1]
                : new string(face.Rows[at.Line - 1].Select(cell => cell?.Value ?? '⠀').ToArray()).TrimEnd('⠀');
            return (at.Line, row, true);
        }
        TextEditor? editor = pane.Editor.IsKeyboardFocusWithin ? pane.Editor
            : PageEditor.Editor.IsKeyboardFocusWithin ? PageEditor.Editor
            : SourceEditor.IsKeyboardFocusWithin ? SourceEditor : null;
        if (editor is null) return null;
        var line = editor.Document.GetLineByNumber(editor.TextArea.Caret.Line);
        return (line.LineNumber, editor.Document.GetText(line), editor == pane.Editor);
    }

    private void DisplayLinePreview(int line, string text, bool reverse)
    {
        currentPreview = (line, text, reverse);
        LineStrip.SetPreview(line, text, reverse);
        detachedPane?.SetLinePreview(line, text, reverse);
    }

    private async void QueuePreview(bool sourceChanged)
    {
        if (resourcesReleased || smoke || switching || session?.ActiveBlock is not { } block || engine is not LocalBrailleEngine local) return;
        sourceChanged &= !session.IsReadOnly;
        pendingSourceTranslation |= sourceChanged;
        if (sourceChanged) translator.Invalidate(block);
        var generation = ++previewGeneration;
        debounce?.Cancel(); debounce?.Dispose(); debounce = new();
        var waitToken = debounce.Token; var owner = session; var ct = documentLifetime.Token;
        try
        {
            await Task.Delay(450, waitToken);
            if (resourcesReleased || ct.IsCancellationRequested || generation != previewGeneration || session != owner || session.ActiveBlock != block) return;
            var translate = pendingSourceTranslation; pendingSourceTranslation = false;
            if (translate && !owner.IsReadOnly && autoTranslate && translatedRevisions.GetValueOrDefault(block, -1) != block.SourceRevision)
                await TranslateAsync(block, ChooseTranslation(block), interactive: false);
            if (generation != previewGeneration || !LinePreviewToggle.IsChecked || FocusedPreviewLine() is not { } line) return;
            string text;
            if (line.Reverse)
            {
                var cells = BrailleMarkup.PreviewCells(line.Text);
                text = cells.Length == 0 ? "" : await local.DecodeAsync(cells, owner.Options, ct);
            }
            else text = await local.TranslateLinePreviewAsync(line.Text, owner.Options, ct);
            if (generation == previewGeneration && session == owner && !ct.IsCancellationRequested)
                DisplayLinePreview(line.Line, text, line.Reverse);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or SidecarException or TimeoutException or System.ComponentModel.Win32Exception)
        {
            if (generation != previewGeneration || session != owner) return;
            if (FocusedPreviewLine() is { } line) DisplayLinePreview(line.Line, "", line.Reverse);
            session.Status = "줄번역을 표시하지 못했습니다. 엔진 연결을 확인해 주세요.";
        }
    }

    private void LinePreviewToggle_Click(object sender, RoutedEventArgs e)
    {
        SetLinePreviewVisible(LinePreviewToggle.IsChecked);
    }

    private void SetLinePreviewVisible(bool visible)
    {
        LinePreviewToggle.IsChecked = visible;
        LineStrip.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        detachedPane?.SetLinePreviewVisible(visible);
        QueuePreview(false);
    }

    private async void SelectOriginalImageRegion(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var point = e.GetPosition(OriginalImageGrid);
        var selected = session.Blocks.Where(b => b.Page == DisplayedOriginalPage && b.Metadata.BoundingBox is { Length: 4 } r
            && point.X >= r[0] && point.X <= r[2] && point.Y >= r[1] && point.Y <= r[3])
            .OrderBy(b => (b.Metadata.BoundingBox![2] - b.Metadata.BoundingBox[0]) * (b.Metadata.BoundingBox[3] - b.Metadata.BoundingBox[1])).FirstOrDefault();
        if (selected is null) return;
        try { await SelectOriginalRegionAsync(selected); }
        catch (Exception ex) { ShowError(ex); }
    }
    private EditorBlock? originalRevealTarget;
    private void RevealSelectedOriginal()
    {
        if (originalRevealTarget is not { } block || session.ActiveBlock != block || DisplayedOriginalPage != block.Page
            || !OriginalSelection.IsVisible) return;
        OriginalScroll.UpdateLayout();
        if (OriginalScroll.Template.FindName("PART_ScrollContentPresenter", OriginalScroll)
            is System.Windows.Controls.Primitives.IScrollInfo presenter)
            presenter.MakeVisible(OriginalSelection, new Rect(OriginalSelection.RenderSize));
        originalRevealTarget = null;
    }

    private void SyncOriginalToBlock(EditorBlock block)
    {
        originalRevealTarget = block;
        Original.SelectBlock(block.Id, block.Page);
        if (DisplayedOriginalPage == block.Page) { RevealSelectedOriginal(); return; }
        ShowOriginal(block.Page);
        RefreshEditorStatus();
    }

    private long originalGeneration;
    private async void ShowOriginal(int page)
    {
        DisplayedOriginalPage = page;
        var generation = ++originalGeneration; var owner = session;
        if (smoke || owner.IsConversionFailed(page)) return;
        OriginalEmpty.Visibility = Visibility.Visible;
        OriginalImage.Source = null; OriginalText.Text = "원본 미리보기 없음";
        OriginalText.Visibility = Visibility.Visible;
        OriginalImageGrid.Visibility = Visibility.Collapsed;
        OriginalSelection.Visibility = Visibility.Collapsed;
        if (!session.OriginalPages.TryGetValue(page, out var original)) return;
        if (original.Type == "text") { OriginalText.Text = string.Join("\n", original.Lines ?? []); FinishOriginalWheelTransition(owner, page, OriginalScroll); return; }
        if (original.AssetName is null || !session.OriginalFiles.TryGetValue(original.AssetName, out var bytes)) return;
        try
        {
            BitmapSource image;
            if (original.Type == "pdf") { OriginalText.Text = "PDF 원본을 표시하는 중…"; image = await PdfPreview.RenderAsync(bytes, page, documentLifetime.Token); }
            else
            {
                using var stream = new MemoryStream(bytes, writable: false);
                var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 1800; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); image = bitmap;
            }
            if (generation != originalGeneration || owner != session) return;
            OriginalImage.Source = image;
            OriginalText.Text = ""; OriginalText.Visibility = Visibility.Collapsed;
            OriginalImageGrid.Visibility = Visibility.Visible;
            var width = original.Width > 0 ? original.Width : image.PixelWidth;
            var height = original.Height > 0 ? original.Height : image.PixelHeight;
            OriginalImageGrid.Width = width; OriginalImageGrid.Height = height;
            if (session.ActiveBlock is { } active && active.Page == page && active.Metadata.BoundingBox is { Length: 4 } box && box.All(double.IsFinite))
            {
                var x = Math.Clamp(box[0], 0, width); var y = Math.Clamp(box[1], 0, height);
                OriginalSelection.Width = Math.Max(0, Math.Clamp(box[2], 0, width) - x);
                OriginalSelection.Height = Math.Max(0, Math.Clamp(box[3], 0, height) - y);
                System.Windows.Controls.Canvas.SetLeft(OriginalSelection, x); System.Windows.Controls.Canvas.SetTop(OriginalSelection, y);
                OriginalSelection.Visibility = Visibility.Visible;
                RevealSelectedOriginal();
            }
            FinishOriginalWheelTransition(owner, page, OriginalScroll);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is NotSupportedException or IOException or InvalidDataException or ArgumentException or System.Runtime.InteropServices.COMException)
        { if (generation == originalGeneration && owner == session) { OriginalText.Text = "원본을 표시할 수 없습니다: " + ex.Message; FinishOriginalWheelTransition(owner, page, OriginalScroll); } }
    }
}
