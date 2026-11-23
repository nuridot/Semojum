using ICSharpCode.AvalonEdit;
using Semojum.Core;
using Semojum.Desktop.Dialogs;

namespace Semojum.Desktop;

public partial class MainWindow
{
    private void ShowGoTo()
    {
        var panel = ActiveSearchPanel;
        new GoToDialog(panel, (target, value) => NavigateAsync(target, value, panel)) { Owner = this }.ShowDialog();
    }

    internal async Task<string> NavigateAsync(NavigationTarget target, string input, SearchPanel panel)
    {
        if (target == NavigationTarget.BraillePage)
        {
            var pane = detachedPane ?? Braille;
            var currentFace = pane.DisplayedFace ?? LastPreviewFace;
            var face = DocumentNavigation.Number(input, currentFace);
            if (!pane.GridView.GoToFace(face)) throw new ArgumentException($"점자 면은 1–{pane.GridView.Items.Count} 범위로 입력해 주세요.");
            if (openDocument is not null) openDocument.LastPreview = (session.ChangeVersion, face, pane.PrintedNumberAt(face));
            RefreshEditorStatus(); return $"점자 {face}면으로 이동했습니다.";
        }
        if (session.ActiveBlock is not { } active) return "열린 작업 블록이 없습니다.";
        if (target == NavigationTarget.OriginalPage)
        {
            var original = zoomPanel == ZoomPanel.Original && session.IsTextJob != true;
            var page = DocumentNavigation.Number(input, original ? DisplayedOriginalPage : PageEditor.DisplayedPage);
            if (!session.IsConversionFailed(page) && (!session.Blocks.Any(b => b.Page == page && !b.Metadata.IsBlocked) ||
                original && !session.OriginalPages.ContainsKey(page)))
                throw new ArgumentException("이동할 수 있는 쪽 번호를 입력해 주세요.");
            if (original) BrowseOriginalPage(page); else BrowseSourcePage(page);
            return $"{page}쪽으로 이동했습니다.";
        }
        var editor = SearchEditor(panel);
        var destination = DocumentNavigation.Resolve(session.Blocks, active, panel, editor.CaretOffset, target, input);
        return await NavigateToAsync(destination);
    }

    private async Task<string> NavigateToAsync(NavigationLocation destination, ReviewMark? review = null)
    {
        var owner = session;
        var navigationLifetime = documentLifetime.Token;
        var previous = owner.ActiveBlock;
        if (previous is not null && previous != destination.Block && autoTranslate && !owner.IsReadOnly
            && translatedRevisions.GetValueOrDefault(previous, -1) != previous.SourceRevision)
        {
            var decision = ChooseTranslation(previous);
            if (decision == ManualEditDecision.Cancel) return "이동을 취소했습니다.";
            previewGeneration++; debounce?.Cancel(); pendingSourceTranslation = false;
            await TranslateAsync(previous, decision, interactive: false);
            if (owner != session || navigationLifetime.IsCancellationRequested) return "문서가 바뀌어 이동을 취소했습니다.";
            if (translatedRevisions.GetValueOrDefault(previous, -1) != previous.SourceRevision)
                return owner.Status = "점역 중 내용이 바뀌거나 점역이 완료되지 않아 이동하지 않았습니다.";
        }
        if (navigationLifetime.IsCancellationRequested || owner != session || !owner.Blocks.Contains(destination.Block))
            return "이동할 블록이 변경되어 이동을 취소했습니다.";
        var pane = detachedPane ?? Braille;
        if (review is { Panel: ReviewPanel.Braille } && pane.GridView.Visibility == System.Windows.Visibility.Visible && !await pane.EnsureCurrentGridAsync(owner))
            return owner.Status = "점자 조판을 갱신할 수 없어 검토 표시로 이동하지 못했습니다.";
        if (owner != session || !owner.Blocks.Contains(destination.Block)) return "이동할 블록이 변경되었습니다.";
        var offset = destination.Offset;
        var length = 0;
        if (review is not null)
        {
            var current = destination.Block.ReviewMarks.FirstOrDefault(m => m.Id == review.Id);
            if (current is null) return owner.Status = "이동할 검토 표시가 변경되었습니다.";
            offset = current.Start;
            length = current.IsStale ? 0 : current.Length;
        }
        session.ActiveBlock = destination.Block;
        var editor = SearchEditor(destination.Panel);
        if (destination.Panel == SearchPanel.Text)
            SourceToggle.IsChecked = true;
        else BrailleToggle.IsChecked = true;
        UpdatePanelLayout();
        offset = Math.Clamp(offset, 0, editor.Document.TextLength);
        length = Math.Clamp(length, 0, editor.Document.TextLength - offset);
        editor.Select(offset, length);
        editor.ScrollToLine(editor.TextArea.Caret.Line);
        if (destination.Panel == SearchPanel.Text && PageEditor.Visibility == System.Windows.Visibility.Visible)
        { PageEditor.FollowActive(offset, length); SetActiveEditor(PageEditor.Editor); }
        else
        {
            SetActiveEditor(editor);
            if (review is not null && destination.Panel == SearchPanel.Braille && (detachedPane ?? Braille).GridView is { IsVisible: true } grid)
            {
                grid.SelectSearchResult(destination.Block, offset, length);
                lastGrid = grid;
                grid.Focus();
            }
        }
        return session.Status = $"원본 {destination.Block.Page}쪽 · 블록 {destination.Block.Id} · {editor.TextArea.Caret.Line}줄로 이동했습니다.";
    }
}
