using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace Semojum.Desktop.Controls;

/// <summary>Presentation only: linked regions never become editable selections.</summary>
internal sealed class RegionHighlight(TextEditor editor, Func<IReadOnlyList<(int Start, int Length)>> ranges) : DocumentColorizingTransformer, IBackgroundRenderer
{
    public KnownLayer Layer => KnownLayer.Selection;
    public void Draw(TextView view, DrawingContext dc) => DrawRows(view, dc, view.ActualWidth);

    internal void DrawRows(TextView view, DrawingContext dc, double width)
    {
        if (!view.VisualLinesValid) return;
        var spans = ranges();
        if (spans.Count == 0) return;
        foreach (var line in view.VisualLines)
        {
            var start = line.FirstDocumentLine.Offset;
            var end = line.LastDocumentLine.EndOffset;
            if (spans.Any(s => s.Length > 0 && s.Start <= start && s.Start + s.Length >= end && start < s.Start + s.Length))
                dc.DrawRectangle(editor.TextArea.SelectionBrush, null, new Rect(0, line.VisualTop - view.VerticalOffset, width, line.Height));
        }
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        foreach (var (start, length) in ranges())
        {
            var from = Math.Max(start, line.Offset);
            var to = Math.Min(start + length, line.EndOffset);
            if (to <= from) continue;
            ChangeLinePart(from, to, element =>
            {
                element.TextRunProperties.SetBackgroundBrush(editor.TextArea.SelectionBrush);
                element.TextRunProperties.SetForegroundBrush(editor.TextArea.SelectionForeground);
            });
        }
    }
}
