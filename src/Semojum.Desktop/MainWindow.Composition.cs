using Semojum.Core;
using Semojum.Core.Integration;
using Semojum.Desktop.Dialogs;

namespace Semojum.Desktop;
public partial class MainWindow
{
    private int LastPreviewFace => openDocument?.LastPreview?.Face ?? 1;
    private async Task ShowCompositionAsync(bool print = false)
    {
        if (engine is not LocalBrailleEngine local) throw new ArgumentException("샘플 문서는 조판할 수 없습니다.");
        if (fileBusy) return;
        fileBusy = true;
        try
        {
            var document = session;
            var tab = openDocument;
            var version = document.ChangeVersion;
            var selectedSource = zoomPanel == ZoomPanel.Original ? DisplayedOriginalPage : PageEditor.DisplayedPage;
            var selectedFace = (detachedPane ?? Braille).DisplayedFace ?? LastPreviewFace;
            var snapshot = SmjFile.Snapshot(session).Manifest;
            DocumentExport.RequireCompletedPages(snapshot);
            var original = DocumentExport.OriginalNumbers(snapshot).Select(n => new SourcePreviewPage(n,
                string.Join("\n", snapshot.Blocks.Where(b => b.Page == n).Select(b => b.Source)))).ToArray();
            var selectedIds = snapshot.Blocks.Select(b => b.Id).ToHashSet();
            if (session.Blocks.Where(b => selectedIds.Contains(b.Id)).Any(b => b.IsBrailleLocked ||
                b.Source.Length > 0 && b.Braille.Length == 0 && !LocalBrailleEngine.IsLayoutMarker(b.Source) && !LocalBrailleEngine.IsNonPrintingTag(b.Source) ||
                b.Source.Length > 0 && !b.HasManualBraille && translatedRevisions.GetValueOrDefault(b, -1) != b.SourceRevision))
            {
                const string message = "점자 면을 보거나 인쇄하려면 원문·옵션이 바뀐 블록을 재점역해 주세요.";
                if (print || original.Length == 0) throw new ArgumentException(message);
                var sourceOnly = new DocumentPreviewWindow(original, [], document.Title, document.IsDirty, selectedSource, 1, true,
                    StatusInput.Text, document.Options.KoreanGrade1, document.Options.EnglishGrade1, _ => { }, message) { Owner = this };
                sourceOnly.ShowDialog();
                return;
            }
            session.Status = "점자 면을 조판하는 중…";
            var result = await local.ComposeAsync(snapshot, documentLifetime.Token);
            if (result.Replaced.Count > 0) throw new ArgumentException("조판에서 빈칸으로 바뀌는 문자가 있습니다: " + string.Join(", ", result.Replaced.Keys) + ". 편집 화면에서 확인해 주세요.");
            var pages = await local.ReadBrfAsync(result.Brf, documentLifetime.Token);
            if (!ReferenceEquals(document, session)) return;
            if (pages.Length == 0) throw new ArgumentException("미리 볼 점자 면이 없습니다.");
            var face = Math.Clamp(selectedFace, 1, pages.Length);
            int ShowFaces(System.Windows.Window owner, int current)
            {
                var dialog = new BraillePreviewDialog(pages, result.Brf, snapshot.Layout, document.Title, current,
                    result.SpacingUnknownBlockIds, result.AlignmentUnknownBlockIds, null) { Owner = owner };
                dialog.ShowDialog(); return dialog.Page;
            }
            if (print) face = ShowFaces(this, face);
            else
            {
                var preview = new DocumentPreviewWindow(original, pages, document.Title, document.IsDirty, selectedSource, face,
                    zoomPanel != ZoomPanel.Braille, StatusInput.Text, document.Options.KoreanGrade1, document.Options.EnglishGrade1,
                    window => window.SetFace(ShowFaces(window, window.Face))) { Owner = this };
                preview.ShowDialog(); face = preview.Face;
            }
            if (tab is not null && ReferenceEquals(tab.Model, document))
                tab.LastPreview = (version, face, result.Pages?.ElementAtOrDefault(face - 1)?.Number);
            document.Status = $"조판 결과 {pages.Length}면 · 마지막 확인 {face}면";
        }
        finally { fileBusy = false; }
    }
}
