using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Semojum.Core;
using Semojum.Core.Integration;
using Semojum.Desktop.Dialogs;
using SharpVectors.Converters;

using static Semojum.Desktop.Services.WindowsSmoke;

namespace Semojum.Desktop.Services;

internal static class WindowsEditorToolChecks
{
    internal static async Task RunAsync(string directory, Action<bool,string> check)
    {
        var folder = Directory.CreateDirectory(Path.Combine(directory, "editor-tools")).FullName;
        await using var sidecar = EngineRuntime.Create();
        var engine = new LocalBrailleEngine(sidecar);
        var symbols = await engine.SymbolsAsync();
        var squareRoot = symbols.First(s => s.Char == "√");
        var window = new MainWindow(smoke: true, testWithoutAccount: true);
        var type = typeof(MainWindow);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetField("engine", flags)!.SetValue(window, engine);
        window.Show();
        try
        {
            var model = window.Session;
            model.Blocks.Clear();
            var first = new EditorBlock { Id = 1, Page = 12, Source = "앞 원문 뒤", Braille = "⠁⠃⠉⠙" };
            var last = new EditorBlock { Id = 2, Page = 14, Source = "마지막" };
            model.Add(first); model.Add(last); model.ActiveBlock = first;
            first.ReviewMarks.Add(new("r1", ReviewKind.SuggestCorrection, ReviewPanel.Text, 2, 2, "예전", true));
            first.ReviewMarks.Add(new("r2", ReviewKind.OpenWarning, ReviewPanel.Text, 0, 0, "예전 경고", true));
            first.ReviewMarks.Add(new("r3", ReviewKind.SuggestDeletion, ReviewPanel.Braille, 1, 1, "⠿", true));
            last.ReviewMarks.Add(new("r4", ReviewKind.OpenWarning, ReviewPanel.Text, 0, 0, "다른 경고", true));
            window.PageEditor.Refresh();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            type.GetMethod("SetActiveEditor", flags)!.Invoke(window, [window.PageEditor.Editor]);

            await WithModal<ReviewListDialog>(() => window.ExecuteAsync("reviewList"), async dialog =>
            {
                var list = dialog.Marks;
                list.SelectedIndex = 1;
                await WindowsSmoke.RenderAsync(dialog, Path.Combine(folder, "review.png"));
                Frame(dialog, 640, 284);
                check(list.Items.Count == 4 && Children<ListBoxItem>(list).All(i => i.ActualHeight == 36)
                    && Children<TextBlock>(list).Count(t => t.Text == "블록을 고쳐 풀림") == 2,
                    "Review list displays each real mark with page, panel, kind and current anchor state");
                Bounds(dialog, (FrameworkElement)list.Parent, 25,57,590,154, "review list");
                Bounds(dialog, ActionButton(dialog, "이동"), 527,235,88,32, "review move");
                check(!list.ContextMenu.Items.OfType<MenuItem>().First().IsEnabled, "A resolved warning cannot be reassigned");
                list.SelectedIndex = 0;
                ActionButton(dialog, "이동").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            });
            check(model.ActiveBlock == first && window.PageEditor.Editor.CaretOffset == 2 && window.PageEditor.Editor.SelectionLength == 0,
                "Moving to a stale mark opens the current anchor without selecting obsolete text");
            first.ReviewMarks.Clear();
            first.AddReviewMark(ReviewKind.HumanConfirmed, ReviewPanel.Text, 2, 2);
            await WithModal<ReviewListDialog>(() => window.ExecuteAsync("reviewList"), dialog =>
            {
                ActionButton(dialog, "이동").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                return Task.CompletedTask;
            });
            check(window.PageEditor.Editor.SelectedText == "원문", "Moving to a live review selects its exact range in the visible text panel");

            await WithModal<SymbolDialog>(() => window.ExecuteAsync("symbol"), async dialog =>
            {
                var list = Children<ListBox>(dialog).Single();
                check(list.Items.Count == symbols.Length, "All catalogue entries returned by the packaged engine are accessible");
                void Filter(string name) => ActionButton(dialog,name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Filter("원"); check(list.Items.Cast<BrailleSymbol>().Any(s => s.Char == "①"), "Circled-symbol filter includes real circled numbers");
                Filter("괄호"); check(list.Items.Cast<BrailleSymbol>().Any(s => s.Char == "("), "Bracket filter includes opening punctuation");
                Filter("화살표"); check(list.Items.Cast<BrailleSymbol>().All(s => s.Group == "화살표") && list.Items.Count > 0, "Arrows filter uses the engine group");
                Filter("단위"); check(list.Items.Cast<BrailleSymbol>().Any(s => s.Char == "㎏"), "Units include the engine's kilogram symbol");
                Filter("수학");
                list.SelectedItem = list.Items.Cast<BrailleSymbol>().Single(s => s == squareRoot);
                await WindowsSmoke.RenderAsync(dialog, Path.Combine(folder, "symbol.png"));
                Frame(dialog, 600,364);
                var tile = Children<ListBoxItem>(list).Single(i => Equals(i.Content, squareRoot));
                Bounds(dialog, tile, 342,110,40,40, "square-root tile");
                var extras = list.Items.Cast<BrailleSymbol>().Where(s => s.Char == "∝").ToArray();
                check(extras.Length == 1 && Children<ListBoxItem>(list).Single(i => Equals(i.Content, extras[0])).TranslatePoint(new Point(), dialog).Y >= 242,
                    "Additional engine math symbols start on a separate scrollable row after the Figma palette");
                check(Children<TextBlock>(dialog).Any(t => t.Text == squareRoot.Cells), "The selected square root previews the engine's actual braille cells");
                ActionButton(dialog, "넣기").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            });
            check(first.Source == "앞 √ 뒤", "Accepting the symbol replaces the selected source range in the active document");

            var text = Enumerable.Repeat("",10).ToArray();
            var dots = Enumerable.Repeat("",10).ToArray();
            text[0] = "<!주>그림 설명<!/주>"; text[1] = "※ 원문에 없는 내용";
            dots[0] = "⠠⠄⠀⠠⠄"; dots[1] = "⠶⠶⠶⠶";
            var prefs = new AccountPreferences(TextSnippets:text, BrailleSnippets:dots);
            var snippet = new SnippetDialog(prefs);
            await WithModal<SnippetDialog>(() => { snippet.ShowDialog(); return Task.CompletedTask; }, async dialog =>
            {
                await WindowsSmoke.RenderAsync(dialog, Path.Combine(folder, "snippet.png"));
                Frame(dialog, 760,574);
                var fields = Children<TextBox>(dialog).ToDictionary(AutomationProperties.GetName);
                check(fields.Count == 20 && fields.Values.All(f => f.IsVisible), "All twenty actual editable shortcut slots are simultaneously visible");
                Bounds(dialog, fields["Ctrl+0"],89,97,271,32,"first text slot");
                Bounds(dialog, fields["Alt+9"],452,457,271,32,"last braille slot");
                fields["Ctrl+9"].Text = "첫 줄\r\n둘째 줄";
                fields["Alt+9"].Text = "잘못된 점자";
                ActionButton(dialog, "확인").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check(dialog.IsVisible && dialog.Result == prefs && Children<TextBlock>(dialog).Any(t => t.Text.Contains("점자 상용구에는")),
                    "Invalid braille leaves the form open and preserves all saved preferences");
                fields["Alt+9"].Text = "⠉⠙";
                ActionButton(dialog, "확인").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            });
            check(snippet.DialogResult == true && snippet.Result.TextSnippets![9] == "첫 줄\r\n둘째 줄" && snippet.Result.BrailleSnippets![9] == "⠉⠙",
                "Confirming keeps multiline text and the corresponding edited braille slot");
            type.GetField("accountPreferences",flags)!.SetValue(window, snippet.Result);
            window.PageEditor.Editor.Select(2,1);
            var binding = window.InputBindings.OfType<KeyBinding>().Single(k => k.Key == Key.D1 && k.Modifiers == ModifierKeys.Control);
            await ((IAsyncRelayCommand)binding.Command).ExecuteAsync(null);
            check(first.Source == "앞 ※ 원문에 없는 내용 뒤", "The actual Ctrl+1 binding inserts the saved text at the editor selection");

            first.Braille = "⠁⠃⠉⠙";
            first.ReviewMarks.Clear();
            var brailleMark = first.AddReviewMark(ReviewKind.SuggestCorrection, ReviewPanel.Braille, 1, 2);
            async Task GoToBrailleMark()
            {
                await WithModal<ReviewListDialog>(() => window.ExecuteAsync("reviewList"), dialog =>
                {
                    dialog.Marks.SelectedItem = dialog.Marks.Items.Cast<ReviewListDialog.Item>()
                        .Single(i => i.Mark.Id == brailleMark.Id);
                    ActionButton(dialog, "이동").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    return Task.CompletedTask;
                });
            }
            await GoToBrailleMark();
            var grid = window.Braille.GridView;
            check(grid.IsVisible && grid.Selection is [{ Start: 1, Length: 2 }],
                "A braille review opens its two selected cells in the visible braille grid");
            await WithModal<SymbolDialog>(() => window.ExecuteAsync("symbol"), dialog =>
            {
                var list = Children<ListBox>(dialog).Single();
                list.SelectedItem = list.Items.Cast<BrailleSymbol>().First(s => s == squareRoot);
                ActionButton(dialog, "넣기").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                return Task.CompletedTask;
            });
            check(first.Braille == "⠁" + squareRoot.Cells + "⠙" && first.Source == "앞 ※ 원문에 없는 내용 뒤",
                "Braille symbol insertion replaces only the selected grid cells with the engine result");
            first.ReviewMarks.Clear();
            brailleMark = first.AddReviewMark(ReviewKind.HumanConfirmed, ReviewPanel.Braille, 1, squareRoot.Cells.Length);
            await GoToBrailleMark();
            var beforeShortcut = string.Join("/", grid.Selection.Select(s => $"{s.Start}+{s.Length}"));
            var activeBeforeShortcut = type.GetProperty("ActiveSearchPanel",flags)!.GetValue(window);
            var alt = window.InputBindings.OfType<KeyBinding>().Single(k => k.Key == Key.D9 && k.Modifiers == ModifierKeys.Alt);
            await ((IAsyncRelayCommand)alt.Command).ExecuteAsync(null);
            check(first.Braille == "⠁⠉⠙⠙", $"The actual Alt+9 binding replaces grid selection {beforeShortcut} in {activeBeforeShortcut} with the saved braille (result {first.Braille}, status {model.Status})");
            var empty = new SymbolDialog([]);
            try { empty.Show(); empty.UpdateLayout(); check(!ActionButton(empty,"넣기").IsEnabled, "An empty symbol catalogue cannot submit a blank symbol"); }
            finally { empty.Close(); }
        }
        finally { await window.ReleaseResourcesAsync(); window.PrepareSmokeShutdown(); window.Close(); }

        void Frame(Window dialog, int width, int height)
        {
            check(dialog.ActualWidth == width && dialog.ActualHeight == height, $"{dialog.Title} matches the {width}x{height} Figma frame");
            check(Children<SvgViewbox>(dialog).Any(i => i.IsVisible && i.Source.ToString().EndsWith("/form-close.svg") && i.Drawings is { Bounds.IsEmpty: false }),
                $"{dialog.Title} renders the original Figma close icon");
        }
        void Bounds(Window dialog, FrameworkElement element, int x, int y, int w, int h, string name)
        {
            var p = element.TranslatePoint(new Point(), dialog);
            check(Math.Abs(p.X-x)<=1 && Math.Abs(p.Y-y)<=1 && Math.Abs(element.ActualWidth-w)<=1 && Math.Abs(element.ActualHeight-h)<=1,
                $"{name}: ({p.X},{p.Y}) {element.ActualWidth}x{element.ActualHeight} matches ({x},{y}) {w}x{h}");
        }
    }

    private static Button ActionButton(Window window, string content) => Children<Button>(window).Single(b => Equals(b.Content,content) || b.Content is TextBlock && AutomationProperties.GetName(b) == content);
    private static IEnumerable<T> Children<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i=0; i<VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent,i);
            if (child is T value) yield return value;
            foreach (var item in Children<T>(child)) yield return item;
        }
    }
}
