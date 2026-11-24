using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Reflection;
using Semojum.Desktop.Controls;
using Semojum.Core;
using SharpVectors.Converters;

namespace Semojum.Desktop.Services;

internal static class WindowsLineTranslationDesignChecks
{
    internal static async Task RunEditorAsync(Action<bool, string> check)
    {
        var model = new DocumentSession();
        var manual = "⠉\n⠊⠗⠚⠒⠑⠟⠈⠍⠁\n" + new string('⠫', model.Layout.Columns) + "⠉";
        var block = new EditorBlock { Id = 1, Page = 1, Source = "가 나\n<!쪽바꿈>\n<!임의태그>\n가<!묵자쪽번호>나\n<!수식>x+1<!/수식>",
            Braille = manual, HasManualBraille = true };
        model.Add(block);
        var window = new MainWindow(testWithoutAccount: true, startEmpty: true);
        typeof(MainWindow).GetMethod("SetSession", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(window, [model, null, true, false]);
        window.Show();
        try
        {
            var editor = window.PageEditor.Editor;
            editor.Focus();
            await Expect("줄 점역", "⠀⠀⠫⠀⠉", "Text focus previews the current source line, including its paragraph indent and word space");
            editor.CaretOffset = editor.Document.GetLineByNumber(2).Offset;
            await Expect("줄 점역", "", "A page-break tag has no cells in the preview");
            editor.CaretOffset = editor.Document.GetLineByNumber(4).Offset;
            await Expect("줄 점역", "⠀⠀⠫⠉", "A deferred layout field cannot leak print text into forward preview");
            editor.CaretOffset = editor.Document.GetLineByNumber(3).Offset;
            await Expect("줄 점역", "", "An unsupported tag alone does not display the engine's failure notice as braille");
            editor.CaretOffset = editor.Document.GetLineByNumber(5).Offset;
            await Expect("줄 점역", "⠀⠀⠭⠢⠼⠁", "A supported math tag still produces its braille cells");
            var grid = window.Braille.GridView;
            check(await window.Braille.EnsureCurrentGridAsync(model), "The real braille grid has composed lines");
            grid.Focus();
            await Expect("줄 역점역", "나", "Braille focus decodes the manually edited grid row independently of the source");
            var next = grid.Items.Cast<BrailleGridFace>().SelectMany(f => f.Rows).SelectMany(row => row)
                .First(c => c?.Block == block && c.Offset == 2)!;
            grid.Point(next, false);
            await Expect("줄 역점역", "대한민국", "Moving the braille caret decodes the new row");
            window.ToggleDetached();
            await Until(() => window.DetachedPane!.PreviewStrip.LineLabel.Text == "줄 역점역"
                && window.DetachedPane.PreviewStrip.Cells.Text.Trim() == "대한민국", "Detached braille mirrors the decoded line");
            var detached = window.DetachedPane!;
            check(await detached.EnsureCurrentGridAsync(model), "Detached grid has current physical rows");
            detached.GridView.Focus();
            var wrapped = detached.GridView.Items.Cast<BrailleGridFace>().SelectMany(f => f.Rows).SelectMany(row => row)
                .First(c => c?.Block == block && c.Offset == manual.Length - 1)!;
            detached.GridView.Point(wrapped, false);
            await Until(() => detached.PreviewStrip.IsVisible && detached.PreviewStrip.Cells.Text.Trim() == "나"
                && window.LineStrip.Cells.Text.Trim() == "나", "Detached caret movement decodes only the current soft-wrapped row in both strips");
            window.ToggleDetached();
            window.Activate();
            editor.Focus();
            editor.CaretOffset = 0;
            await Expect("줄 점역", "⠀⠀⠫⠀⠉", "Returning to text restores forward preview");
            check(block.HasManualBraille && block.Braille == manual, "Previewing either direction preserves the edited braille");
        }
        finally { await window.ReleaseResourcesAsync(); window.PrepareSmokeShutdown(); window.Close(); }

        Task Expect(string label, string content, string name) => Until(() => window.LineStrip.LineLabel.Text == label
            && window.LineStrip.Cells.Text.Trim(' ') == content, name);
        async Task Until(Func<bool> ready, string name)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (!ready() && timer.Elapsed < TimeSpan.FromSeconds(12)) await Task.Delay(40);
            check(ready(), name + (ready() ? "" : $" (displayed {window.LineStrip.LineLabel.Text}: [{window.LineStrip.Cells.Text}])"));
        }
    }

    internal static void Run(Window owner, Action<bool, string> check)
    {
        var pane = new BraillePane { Width = 820, Height = 420 }; pane.SetLinePreview(7, "⠼⠉⠀⠦⠊⠴"); pane.SetLinePreviewVisible(true);
        var closed = false; pane.PreviewCloseRequested += (_, _) => { closed = true; pane.SetLinePreviewVisible(false); };
        var window = new Window { Width = 820, Height = 420, WindowStyle = WindowStyle.None, Content = pane, Owner = owner }; window.Show();
        try
        {
            window.UpdateLayout(); var strip = pane.PreviewStrip;
            var close = Children(strip).OfType<Button>().Single(b => AutomationProperties.GetName(b) == "줄번역 숨기기");
            var icon = close.Content as SvgViewbox;
            check(strip.ActualHeight == 56 && strip.Cells.Text == "⠼⠉⠀⠦⠊⠴" && strip.LineLabel.Text == "줄 점역", "Braille panel line translation displays the supplied cells at the Figma height");
            check(icon is { Width: 16, Height: 16, Drawings.Bounds.IsEmpty: false } && icon.Source?.ToString().EndsWith("line-preview-close.svg", StringComparison.Ordinal) == true && icon.ActualWidth > 0 && icon.ActualWidth <= 16 && icon.ActualHeight > 0 && icon.ActualHeight <= 16, "Line translation uses the exact Figma close SVG in a 16px slot");
            check(pane.GridView.IsVisible, "Braille grid remains visible while line translation is open");
            pane.SetLinePreview(8, "⠁\n⠃\n⠉");
            void Layout()
            {
                window.UpdateLayout();
                window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
            Layout();
            check(strip.NextLine.IsEnabled && !strip.PreviousLine.IsEnabled, "C4 three-line preview starts at the first row");
            strip.NextLine.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout();
            check(Math.Abs(strip.Viewport.VerticalOffset - 28) < 1 && strip.PreviousLine.IsEnabled, "Down advances one visible braille row");
            strip.NextLine.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout();
            check(!strip.NextLine.IsEnabled && strip.PreviousLine.IsEnabled, "Navigation stops at the last braille row");
            strip.PreviousLine.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout();
            check(Math.Abs(strip.Viewport.VerticalOffset - 28) < 1, "Up returns one braille row");
            pane.SetLinePreview(9, new string('⠿', 250)); Layout();
            check(strip.NextLine.IsEnabled && !strip.PreviousLine.IsEnabled && strip.Viewport.VerticalOffset == 0, "An unbroken long translation wraps and starts at its first row");
            close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(closed && strip.Visibility == Visibility.Collapsed && pane.GridView.IsVisible, "Closing line translation leaves the braille editor available");
        }
        finally { window.Close(); pane.Dispose(); }

        var global = new LineTranslation(); global.SetPreview(3, "⠁⠃");
        check(global.Height == 56 && global.Cells.Text == "⠁⠃", "The shared editor strip uses the C4 56px height and supplied translation");

        var editor = new MainWindow(smoke: true, testWithoutAccount: true); editor.Show();
        try
        {
            editor.ApplyAppearance(new());
            check(editor.Braille.GridView.CurrentLineBrush is SolidColorBrush { Color: var initial } && initial == Color.FromRgb(242, 246, 251), "Default braille current line uses the final Figma color");
            bool TextLineIs(Color expected) => editor.SourceEditor.TextArea.TextView.CurrentLineBackground is SolidColorBrush source && source.Color == expected
                && editor.PageEditor.Editor.TextArea.TextView.CurrentLineBackground is SolidColorBrush page && page.Color == expected;
            check(TextLineIs(Color.FromRgb(242, 246, 251)), "New text and source editors use the final Figma current-line color");
            editor.ApplyAppearance(new EditorAppearance(CurrentLineColor: "#123456"));
            check(TextLineIs(Color.FromRgb(18, 52, 86)), "An unsaved text current-line color is previewed");
            editor.ApplyAppearance(new());
            check(TextLineIs(Color.FromRgb(242, 246, 251)), "Canceling the preview restores the new default text current-line color");
            var stored = new EditorAppearance(CurrentLineColor: "#FFF5A3");
            typeof(MainWindow).GetField("accountPreferences", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(editor, new AccountPreferences(Appearance: stored));
            editor.ApplyAppearance(stored);
            check(editor.Braille.GridView.CurrentLineBrush is SolidColorBrush { Color: var saved } && saved == Color.FromRgb(255, 245, 163), "A saved yellow current-line color is preserved");
            check(TextLineIs(Color.FromRgb(255, 245, 163)), "Saved yellow current-line colors remain on text and source editors");
            var custom = stored with { CurrentLineColor = "#123456" }; editor.ApplyAppearance(custom);
            check(TextLineIs(Color.FromRgb(18, 52, 86)), "A saved user's text current-line color can be previewed");
            editor.ApplyAppearance(custom with { HighContrast = true });
            check(TextLineIs(Color.FromRgb(32, 32, 32)), "High contrast retains its current-line color");
            editor.ApplyAppearance(custom);
            check(editor.Braille.GridView.CurrentLineBrush is SolidColorBrush { Color: var customColor } && customColor == Color.FromRgb(18, 52, 86), "A user preview color is applied to the braille grid");
            editor.ToggleDetached();
            check(editor.DetachedPane?.GridView.CurrentLineBrush is SolidColorBrush { Color: var detached } && detached == Color.FromRgb(18, 52, 86), "A detached braille panel retains the current-line color");
            editor.ToggleDetached();
        }
        finally { editor.PrepareSmokeShutdown(); editor.Close(); }
    }
    private static IEnumerable<DependencyObject> Children(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var nested in Children(child)) yield return nested; }
    }
}
