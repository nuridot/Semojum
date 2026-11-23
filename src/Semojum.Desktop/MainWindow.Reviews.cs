using System.Windows;
using Semojum.Desktop.Dialogs;
using Semojum.Core;

namespace Semojum.Desktop;

public partial class MainWindow
{
    private IReadOnlyList<(int Start, ReviewMark Mark)> ActiveReviewRanges(ReviewPanel panel) => session?.ActiveBlock is { } block
        ? block.ReviewMarks.Where(m => m.Panel == panel && !m.IsStale).Select(m => (m.Start, m)).ToArray() : [];
    private BlockSpan[] CurrentReviewSelection()
    {
        if (lastGrid is not null) return lastGrid.Selection.Where(s => s.Length > 0).ToArray();
        if (lastEditor == PageEditor.Editor) return PageEditor.Selection.Where(s => s.Length > 0).ToArray();
        if (session.ActiveBlock is not { } block || lastEditor is null || lastEditor.SelectionLength == 0) return [];
        return [new BlockSpan(block, lastEditor.SelectionStart, lastEditor.SelectionLength)];
    }

    private ReviewPanel CurrentReviewPanel() => ActiveSearchPanel == SearchPanel.Text ? ReviewPanel.Text : ReviewPanel.Braille;

    private bool ValidateReviewSelection(BlockSpan[] spans, ReviewPanel panel)
    {
        if (session.IsReadOnly) { session.Status = "읽기 전용 문서에는 검토 표시를 변경할 수 없습니다."; return false; }
        if (spans.Length == 0) { session.Status = "먼저 검토할 텍스트 또는 점자를 선택해 주세요."; return false; }
        if (spans.Select(s => s.Block.Page).Distinct().Count() != 1 || spans.Any(s => !session.Blocks.Contains(s.Block)
            || s.Block.Metadata.IsBlocked || panel == ReviewPanel.Braille && s.Block.IsBrailleLocked))
        { session.Status = "선택한 범위에 편집할 수 없는 블록이 있습니다."; return false; }
        foreach (var span in spans)
        {
            var text = panel == ReviewPanel.Text ? span.Block.Source : span.Block.Braille;
            var end = span.Start + span.Length;
            if (span.Start < 0 || span.Length <= 0 || end > text.Length || SplitsCharacter(text, span.Start) || SplitsCharacter(text, end))
            { session.Status = "문자 중간이 포함되어 있습니다. 범위를 다시 선택해 주세요."; return false; }
        }
        return true;
    }

    private static bool SplitsCharacter(string text, int offset) => offset > 0 && offset < text.Length
        && char.IsHighSurrogate(text[offset - 1]) && char.IsLowSurrogate(text[offset]);

    private void ChangeReviews(EditorBlock focus, IEnumerable<EditorBlock> affected, Action change)
    {
        var blocks = affected.Distinct().ToArray();
        var before = blocks.ToDictionary(b => b, b => b.ReviewMarks.ToArray());
        change();
        var after = blocks.ToDictionary(b => b, b => b.ReviewMarks.ToArray());
        if (blocks.All(b => before[b].SequenceEqual(after[b]))) return;
        documents.RecordReviewEdit(focus, before, after);
        RefreshReviewDisplays();
    }

    private void SetReviewSelection(ReviewKind kind)
    {
        var panel = CurrentReviewPanel(); var spans = CurrentReviewSelection();
        if (!ValidateReviewSelection(spans, panel)) return;
        ChangeReviews(spans[0].Block, spans.Select(s => s.Block), () =>
        {
            foreach (var span in spans)
            {
                // A new choice replaces only the exact same range. Partly overlapping
                // reviews remain separately available in the list rather than being lost.
                foreach (var old in span.Block.ReviewMarks.Where(m => !m.IsStale && m.Panel == panel && m.Kind != ReviewKind.OpenWarning
                    && m.Start == span.Start && m.Length == span.Length).ToArray()) span.Block.ReviewMarks.Remove(old);
                span.Block.AddReviewMark(kind, panel, span.Start, span.Length);
            }
        });
        session.Status = $"{ReviewLabel(kind)} 표시를 선택 범위에 저장했습니다. 로컬 SMJ에 저장해 주세요.";
    }

    private void RemoveSelectedReviews()
    {
        var panel = CurrentReviewPanel(); var spans = CurrentReviewSelection();
        if (!ValidateReviewSelection(spans, panel)) return;
        ChangeReviews(spans[0].Block, spans.Select(s => s.Block), () =>
        {
            foreach (var span in spans)
                foreach (var mark in span.Block.ReviewMarks.Where(m => !m.IsStale && m.Panel == panel
                    && m.Start < span.Start + span.Length && span.Start < m.Start + m.Length).ToArray()) RemoveReview(span.Block, mark);
        });
        session.Status = "선택 범위와 겹친 검토 표시를 지웠습니다. 실행 취소로 복원할 수 있습니다.";
    }

    internal static string ReviewLabel(ReviewKind kind) => kind switch
    {
        ReviewKind.SuggestCorrection => "고침 제안",
        ReviewKind.SuggestDeletion => "지울 제안",
        ReviewKind.HumanConfirmed => "사람이 확인함",
        ReviewKind.OpenWarning => "안 풀린 경고",
        _ => "검토 표시"
    };

    // A removed server warning stays as a resolved record so the same server report cannot bring it back.
    private static void RemoveReview(EditorBlock block, ReviewMark mark)
    {
        if (!ServerWarnings.IsServerWarning(mark)) { block.ReviewMarks.Remove(mark); return; }
        var index = block.ReviewMarks.IndexOf(mark);
        if (index >= 0) block.ReviewMarks[index] = ServerWarnings.Resolved(mark);
    }

    private async void ShowReviewList()
    {
        try { await ShowReviewListAsync(); }
        catch (Exception ex) { session.Status = ex.Message; }
    }

    private async Task ShowReviewListAsync()
    {
        var model = session;
        var dialog = CreateReviewListWindow();
        if (dialog.ShowDialog() != true || !ReferenceEquals(model, session) || dialog.Destination is not { } item) return;
        var mark = item.Block.ReviewMarks.FirstOrDefault(m => m.Id == item.Mark.Id);
        if (mark is null) { session.Status = "검토 표시가 변경되어 이동할 수 없습니다."; return; }
        var panel = mark.Panel == ReviewPanel.Text ? SearchPanel.Text : SearchPanel.Braille;
        await NavigateToAsync(new(item.Block, panel, mark.Start), mark);
    }

    internal ReviewListDialog CreateReviewListWindow()
    {
        var originalSelection = CurrentReviewSelection();
        var selectedPanel = CurrentReviewPanel();
        var capturedSession = session;
        var capturedVersion = session.ChangeVersion;
        return new ReviewListDialog(session, Reassign, Remove) { Owner = this };

        string Reassign(ReviewListDialog.Item entry)
        {
            if (!ReferenceEquals(capturedSession, session) || capturedSession.ChangeVersion != capturedVersion)
                return "문서가 변경되어 기존 선택을 사용할 수 없습니다. 창을 닫고 범위를 다시 선택해 주세요.";
            if (!entry.Mark.IsStale || entry.Mark.Kind == ReviewKind.OpenWarning)
                return "다시 확인이 필요한 수동 표시를 선택해 주세요.";
            if (!ValidateReviewSelection(originalSelection, selectedPanel) || selectedPanel != entry.Mark.Panel
                || originalSelection.Length != 1 || originalSelection[0].Block != entry.Block)
                return session.Status = "같은 블록과 패널에서 새 범위를 선택한 뒤 다시 지정해 주세요.";
            var span = originalSelection[0];
            ChangeReviews(entry.Block, [entry.Block], () =>
            {
                entry.Block.ReviewMarks.Remove(entry.Mark);
                entry.Block.AddReviewMark(entry.Mark.Kind, selectedPanel, span.Start, span.Length);
            });
            return session.Status = "선택 범위에 검토 표시를 다시 지정했습니다.";
        }
        string Remove(ReviewListDialog.Item entry)
        {
            if (!ReferenceEquals(capturedSession, session)) return "문서가 변경되어 표시를 지울 수 없습니다.";
            if (session.IsReadOnly) return "읽기 전용 문서에는 검토 표시를 변경할 수 없습니다.";
            ChangeReviews(entry.Block, [entry.Block], () => RemoveReview(entry.Block, entry.Mark));
            return session.Status = "검토 표시를 지웠습니다. 실행 취소로 복원할 수 있습니다.";
        }
    }

    private void RefreshReviewDisplays()
    {
        PageEditor.Editor.TextArea.TextView.Redraw(); SourceEditor.TextArea.TextView.Redraw();
        Braille.GridView.InvalidateFaces(); detachedPane?.GridView.InvalidateFaces();
    }
}
