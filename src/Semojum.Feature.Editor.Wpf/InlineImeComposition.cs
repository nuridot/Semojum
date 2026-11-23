using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace Semojum.Desktop.Services;

// A visual element participates in wrapping and scrolling without inserting
// unfinished syllables into the page model, translation engine or undo history.
internal sealed class InlineImeComposition(TextEditor editor) : VisualLineElementGenerator
{
    private string text = "";
    private int offset, selectionLength;
    private ITextSourceVersion? version;

    internal void Begin()
    {
        End();
        RememberSelection();
    }

    private void RememberSelection()
    {
        version = editor.Document.Version;
        offset = editor.SelectionStart;
        selectionLength = editor.SelectionLength;
    }

    private bool SameTarget => version == editor.Document.Version
        && offset == editor.SelectionStart && selectionLength == editor.SelectionLength;

    internal void Update(string? result, string? composing)
    {
        // Some input methods send a result without a preceding START.
        if (version is null) RememberSelection();
        if (!SameTarget) { Show(""); return; }
        if (result is not null)
        {
            Show("");
            if (result.Length > 0) editor.TextArea.PerformTextInput(result);
            RememberSelection();
        }
        if (composing is not null) Show(composing);
    }

    internal void End()
    {
        Show("");
        version = null;
    }

    private void Show(string value)
    {
        if (value == text) return;
        text = value;
        editor.TextArea.TextView.Redraw(offset, 0);
    }

    public override int GetFirstInterestedOffset(int startOffset) =>
        text.Length != 0 && SameTarget && startOffset <= offset
        && offset <= CurrentContext.VisualLine.LastDocumentLine.EndOffset ? offset : -1;

    public override VisualLineElement ConstructElement(int startOffset)
    {
        // Do not collapse document lines when replacing a multi-line selection.
        var lineEnd = CurrentContext.VisualLine.LastDocumentLine.EndOffset;
        return new PreeditElement(text, Math.Min(selectionLength, lineEnd - startOffset));
    }

    internal sealed class PreeditElement(string text, int documentLength) : FormattedTextElement(text, documentLength);
}
