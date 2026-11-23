using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Semojum.Core;
using Semojum.Desktop.Dialogs;
using SharpVectors.Converters;
using static Semojum.Desktop.Services.WindowsExportRangeChecks;

namespace Semojum.Desktop.Services;

internal static class WindowsNavigationDialogChecks
{
    internal static async Task RunAsync(string directory, Action<bool, string> check)
    {
        var editor = new MainWindow(smoke: true);
        editor.Show();
        try
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            const string word = "강수량", cells = "⠠⠎⠼⠥⠥⠍";
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(MainWindow).GetField("autoTranslate", flags)!.SetValue(editor, false);
            var document = new DocumentSession { IsTextJob = true, Layout = new(Columns: 20, Lines: 4, PageNumbers: "출력 안 함") };
            var first = new EditorBlock
            {
                Id = 1,
                Page = 1,
                Source = string.Join(" ", Enumerable.Repeat(word, 12)),
                Braille = string.Join("\n", Enumerable.Repeat(cells, 12)),
                HasManualBraille = true
            };
            document.Add(first);
            for (int i = 2; i <= 4; i++) document.Add(new EditorBlock { Id = i, Page = i, Source = "다른 쪽", Braille = "⠁" });
            typeof(MainWindow).GetMethod("SetSession", flags)!.Invoke(editor, [document, null, false, false]);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            await Open<GoToDialog>("goto", async dialog =>
            {
                var number = dialog.Number;
                var source = Choice(dialog, "원본 쪽");
                var braille = Choice(dialog, "점자 면");
                check(dialog.ActualWidth == 480 && dialog.ActualHeight == 218 && Bounds(number, dialog) == new Rect(173, 101, 266, 32),
                    $"Go-to frame and number field match Figma: {Bounds(number, dialog)}");
                check(Tree(dialog).OfType<SvgViewbox>().Count(i => i.IsVisible && i.Drawings is { Bounds.IsEmpty: false }) == 1,
                    "Go-to uses the Figma close icon and a plain relative-number field");
                source.IsChecked = true;
                number.Text = "+3"; KeyPress(number, Key.Up); check(number.Text == "+4", "Stepping preserves the relative plus sign");
                KeyPress(number, Key.Down); check(number.Text == "+3", "Down restores the chosen relative offset");
                number.Select(number.Text.Length, 0); source.Focus();
                await WindowsSmoke.RenderAsync(dialog, Path.Combine(directory, "go-to.png"));
                Click(dialog, "이동");
                check(editor.PageEditor.DisplayedPage == 4 && dialog.Result.Text.Contains("4쪽"), "Go-to moves the real source panel three pages forward");
                braille.IsChecked = true; number.Text = "2"; Click(dialog, "이동");
                check(editor.Braille.GridView.CaretCoordinates?.Face == 2, "Go-to moves the real grid to physical face two");
                number.Text = "+1"; Click(dialog, "이동");
                check(editor.Braille.GridView.CaretCoordinates?.Face == 3, "Relative face navigation starts at the selected physical face");
                number.Text = "9999"; Click(dialog, "이동");
                check(dialog.IsVisible && dialog.Result.Text.Contains("범위") && editor.Braille.GridView.CaretCoordinates?.Face == 3,
                    "Invalid face shows feedback without changing the current face");
            });

            var shots = Directory.CreateDirectory(Path.Combine(directory, "find")).FullName;
            await Open<FindDialog>("find", async dialog =>
            {
                var query = Field("찾을 내용");
                var replacement = Field("바꿀 내용");
                var toggle = Tree(dialog).OfType<ToggleButton>().Single(b => AutomationProperties.GetName(b) == "점자 입력");
                check(dialog.ActualWidth == 520 && dialog.ActualHeight == 262 &&
                      Bounds(query, dialog) == new Rect(149, 69, 286, 32) && Bounds(toggle, dialog) == new Rect(447, 69, 32, 32)
                      && Bounds(replacement, dialog) == new Rect(149, 113, 330, 32), "Search and replacement fields and six-dot toggle match their Figma bounds");
                foreach (var (state, isBraille, search) in new[] { ("source", false, word), ("braille", true, cells) })
                {
                    toggle.IsChecked = isBraille; query.Text = search;
                    document.ActiveBlock = first;
                    var panel = isBraille ? editor.Braille.Editor : editor.SourceEditor;
                    panel.Select(0, 0);
                    Click(dialog, "다음 찾기"); Click(dialog, "다음 찾기"); KeyPress(query, Key.F3);
                    check(panel.SelectedText == search && Tree(dialog).OfType<TextBlock>().Any(t => t.IsVisible && t.Text == "3 / 12"),
                        state + " search selects the third actual result of twelve and reports its index");
                    query.Focus(); query.Select(query.Text.Length, 0);
                    await WindowsSmoke.RenderAsync(dialog, Path.Combine(shots, state + ".png"));
                }
                replacement.Text = "⠿";
                Click(dialog, "바꾸기");
                check(first.Braille.Count(c => c == '⠿') == 1 && first.Source.Contains(word), "Single replace changes the selected braille match only");
                Click(dialog, "모두 바꾸기");
                check(first.Braille.Count(c => c == '⠿') == 12 && !first.Braille.Contains(cells) && first.Source.Contains(word),
                    "Replace all changes the remaining eleven braille matches and preserves the source");
                TextBox Field(string label) => Tree(dialog).OfType<TextBox>().Single(b => AutomationProperties.GetName(b) == label);
            });
            WindowsEditorDialogDesignChecks.Run(editor, check);
        }
        finally { await editor.ReleaseResourcesAsync(); editor.PrepareSmokeShutdown(); editor.Close(); }

        async Task Open<T>(string command, Func<T, Task> inspect) where T : Window
        {
            var interaction = editor.Dispatcher.InvokeAsync(async () =>
            {
                var dialog = editor.OwnedWindows.OfType<T>().Single();
                try { await inspect(dialog); }
                finally { if (dialog.IsVisible) dialog.Close(); }
            }, DispatcherPriority.ApplicationIdle);
            await editor.ExecuteAsync(command);
            await interaction.Task.Unwrap();
        }
    }

    private static Rect Bounds(FrameworkElement control, Window owner) => new(control.TranslatePoint(new Point(), owner), control.RenderSize);
    private static void Click(Window dialog, string label) => Tree(dialog).OfType<Button>().Single(b => Equals(b.Content, label))
        .RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    private static void KeyPress(UIElement input, Key key) => input.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(input)!, 0, key)
    { RoutedEvent = Keyboard.PreviewKeyDownEvent });
}
