using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;

namespace Semojum.Desktop.Services;

internal static class WindowsInlineImeChecks
{
    internal static async Task RunAsync(MainWindow owner, Action<bool, string> check)
    {
        var editor = new TextEditor { Text = "앞뒤" };
        NativeIme.Attach(editor);
        var window = new Window { Owner = owner, Content = editor, Width = 320, Height = 120 };
        window.Show();
        try
        {
            editor.Focus();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var composition = editor.TextArea.TextView.ElementGenerators.OfType<InlineImeComposition>().Single();
            var changes = 0;
            editor.TextChanged += (_, _) => changes++;
            FormattedTextElement? VisibleComposition()
            {
                window.UpdateLayout();
                var view = editor.TextArea.TextView;
                view.EnsureVisualLines();
                return view.VisualLines.SelectMany(l => l.Elements).OfType<FormattedTextElement>().SingleOrDefault();
            }
            editor.CaretOffset = 1;
            composition.Begin();
            foreach (var syllable in new[] { "ㅎ", "하", "한" })
            {
                composition.Update(null, syllable);
                check(VisibleComposition() is { RelativeTextOffset: 1, DocumentLength: 0 }
                    && editor.Text == "앞뒤" && changes == 0, $"Inline preedit {syllable} occupies the caret in the rendered line without editing the document");
            }
            composition.Update("한", "ㄱ");
            check(editor.Text == "앞한뒤" && changes == 1 && VisibleComposition()?.RelativeTextOffset == 2,
                "A combined result and next syllable commits once and advances the inline preview");
            composition.End();
            check(VisibleComposition() is null && changes == 1, "Cancel removes only unfinished text");
            editor.Select(1, 2);
            composition.Begin(); composition.Update(null, "글");
            check(VisibleComposition() is { RelativeTextOffset: 1, DocumentLength: 2 } && editor.Text == "앞한뒤",
                "Composition visually replaces the selection before any document edit");
            composition.Update("글", null); composition.End();
            check(editor.Text == "앞글" && changes == 2, "The final result replaces the selection exactly once");
            editor.Undo();
            check(editor.Text == "앞한뒤", "Undo restores the whole selection without an intermediate syllable");
            editor.Select(editor.Document.TextLength, 0);
            composition.Begin(); composition.Update(null, "ㄴ");
            check(VisibleComposition()?.RelativeTextOffset == editor.Document.TextLength, "A syllable is visible at end of line");
            editor.Document.Insert(0, "새");
            composition.Update("늦", null);
            check(editor.Text == "새앞한뒤" && VisibleComposition() is null, "A stale composition cannot commit into changed text");
            composition.End();

            var layouts = new nint[GetKeyboardLayoutList(0, null)];
            GetKeyboardLayoutList(layouts.Length, layouts);
            var korean = layouts.FirstOrDefault(h => (h.ToInt64() & 0x3ff) == 0x12);
            if (korean == 0)
            {
                Console.WriteLine("SKIP: Native Korean composition requires an installed Korean input method.");
                return;
            }
            var previous = ActivateKeyboardLayout(korean, 0);
            try
            {
                var handle = new WindowInteropHelper(window).Handle;
                editor.Select(editor.Document.TextLength, 0);
                SendMessageW(handle, 0x010D, 0, 0);
                SendMessageW(handle, 0x010F, '한', 0x6000); // CS_INSERTCHAR | CS_NOMOVECARET
                check(VisibleComposition()?.RelativeTextOffset == editor.Document.TextLength && editor.Text == "새앞한뒤",
                    "The Korean window message displays the unfinished syllable inside the actual AvalonEdit line");
                SendMessageW(handle, 0x010E, 0, 0);
                check(VisibleComposition() is null, "Ending the native composition removes its visual element");
                editor.IsReadOnly = true;
                SendMessageW(handle, 0x010D, 0, 0);
                SendMessageW(handle, 0x010F, '한', 0x6000);
                check(VisibleComposition() is null && editor.Text == "새앞한뒤", "A read-only editor ignores Korean composition input");
                SendMessageW(handle, 0x010E, 0, 0);
                window.Hide();
                await CheckNativePageInputAsync(owner, check);
            }
            finally { if (previous != 0) ActivateKeyboardLayout(previous, 0); }
        }
        finally { window.Close(); }
    }

    private static async Task CheckNativePageInputAsync(MainWindow window, Action<bool, string> check)
    {
        window.Activate();
        var editor = window.PageEditor.Editor;
        editor.Focus();
        editor.Select(0, 0);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var hwnd = new WindowInteropHelper(window).Handle;
        var context = ImmGetContext(hwnd);
        check(context != 0 && editor.TextArea.IsKeyboardFocused, "The document page owns a Windows input context");
        var open = ImmGetOpenStatus(context);
        try
        {
            ImmSetOpenStatus(context, true);
            var original = editor.Text;
            var block = window.Session.ActiveBlock!;
            var originalBlock = block.Source;
            var version = window.Session.ChangeVersion;
            foreach (var syllable in new[] { "ㅎ", "하", "한" })
            {
                check(ImmSetCompositionStringW(context, 9, syllable, 2, 0, 0), $"Windows accepts the native preedit {syllable}");
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                window.UpdateLayout();
                var view = editor.TextArea.TextView;
                view.EnsureVisualLines();
                check(view.VisualLines.SelectMany(l => l.Elements).OfType<InlineImeComposition.PreeditElement>().Count() == 1
                    && editor.Text == original && block.Source == originalBlock && window.Session.ChangeVersion == version,
                    $"The real page renders {syllable} inline without changing its document or history");
            }
            NativeIme.Complete(editor);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            check(editor.Text == "한" + original && block.Source == "한" + originalBlock,
                "Windows commits one complete syllable through the page editor into the document block");
            check(ImmSetCompositionStringW(context, 9, "글", 2, 0, 0), "Windows begins a second syllable");
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            window.RuleSearch.Focus();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            check(editor.Text == "한글" + original && block.Source == "한글" + originalBlock,
                "Moving focus commits the final Korean syllable in the original document block");
        }
        finally { ImmSetOpenStatus(context, open); ImmReleaseContext(hwnd, context); }
    }

    [DllImport("user32.dll")] private static extern int GetKeyboardLayoutList(int count, [Out] nint[]? layouts);
    [DllImport("user32.dll")] private static extern nint ActivateKeyboardLayout(nint layout, uint flags);
    [DllImport("user32.dll")] private static extern nint SendMessageW(nint hwnd, int message, nint wParam, nint lParam);
    [DllImport("imm32.dll")] private static extern nint ImmGetContext(nint hwnd);
    [DllImport("imm32.dll")] private static extern bool ImmReleaseContext(nint hwnd, nint context);
    [DllImport("imm32.dll")] private static extern bool ImmGetOpenStatus(nint context);
    [DllImport("imm32.dll")] private static extern bool ImmSetOpenStatus(nint context, bool open);
    [DllImport("imm32.dll", CharSet = CharSet.Unicode)] private static extern bool ImmSetCompositionStringW(nint context, uint index, string text, uint bytes, nint reading, uint readingBytes);
}
