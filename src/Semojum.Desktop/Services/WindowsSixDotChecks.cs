using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Semojum.Core;
using Semojum.Desktop.Controls;
using Semojum.Desktop.Dialogs;
using SharpVectors.Converters;

namespace Semojum.Desktop.Services;

internal static class WindowsSixDotChecks
{
    internal static async Task RunAsync(string directory, Action<bool, string> check)
    {
        var editor = new MainWindow(smoke: true);
        editor.Show();
        try
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var directoryPath = Directory.CreateDirectory(Path.Combine(directory, "six-dot")).FullName;
            var grid = editor.Braille.GridView;
            var block = new EditorBlock { Id = 1, Page = 1, Source = "검사", Braille = "⠁" };
            grid.Blocks = [block]; grid.ItemsSource = new[] { new BrailleGridFace(1, [[new GridCell(block, 0, '⠁')]]) };
            string? inserted = null;
            grid.ReplaceAsync = (_, value) => { inserted = value; return Task.CompletedTask; };
            editor.Braille.SixDotEnabled = true;
            var presets = new[]
            {
                ("one", "WERUIO", "REWUIO", new[]{ Key.R, Key.E, Key.W, Key.U, Key.I, Key.O }),
                ("two", "SDFJKL", "FDSJKL", new[]{ Key.F, Key.D, Key.S, Key.J, Key.K, Key.L }),
                ("three", "XCVNM,", "VCXNM,", new[]{ Key.V, Key.C, Key.X, Key.N, Key.M, Key.OemComma })
            };
            for (int index = 0; index < presets.Length; index++)
            {
                var (state, displayed, stored, codes) = presets[index];
                await Open(async dialog =>
                {
                    Modes(dialog)[index].IsChecked = true;
                    await WindowsSmoke.RenderAsync(dialog, Path.Combine(directoryPath, state + ".png"));
                    CheckFrame(dialog, displayed, false);
                    CheckButton(dialog, "확인");
                    check(dialog.Keys == string.Join(" ", stored.ToCharArray()), $"Saving preset {index + 1} returns keys in braille dot order");
                });
                for (var dot = 0; dot < codes.Length; dot++)
                    check(Input(codes[dot]) == char.ConvertFromUtf32(0x2800 + (1 << dot)), $"Preset {index + 1} types dot {dot + 1} in the editor grid");
                check(Input(codes[0], codes[2], codes[5]) == "⠥", $"Preset {index + 1} releases the dot 1-3-6 chord as one cell");
            }

            await Open(async dialog =>
            {
                check(Modes(dialog)[2].IsChecked == true, "Reopening preserves the third preset and its comma");
                Modes(dialog)[1].IsChecked = true; Modes(dialog)[3].IsChecked = true;
                await WindowsSmoke.RenderAsync(dialog, Path.Combine(directoryPath, "custom.png"));
                CheckFrame(dialog, "SDFJKL", true);
                CheckButton(dialog, "확인");
            });
            await Open(dialog =>
            {
                check(dialog.UseCustomKeys && dialog.KeyFields.All(f => !f.IsReadOnly), "Explicit custom mode reopens editable even when its keys match preset two");
                var first = dialog.KeyFields[0];
                first.Focus();
                var click = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent };
                first.RaiseEvent(click);
                check(click.Handled && first.SelectionLength == 1, "Selecting an editable key selects its previous assignment");
                string custom = "Q1,4-/";
                for (int i = 0; i < 6; i++) dialog.KeyFields[i].Text = custom[i].ToString();
                dialog.KeyFields[2].Text = "!";
                CheckButton(dialog, "확인");
                check(dialog.IsVisible && Tree(dialog).OfType<TextBlock>().Any(t => t.IsVisible && t.Text.Contains("서로 다른 실제 키")),
                    "A duplicate physical key is rejected without closing the custom fields");
                dialog.KeyFields[2].Text = ",";
                CheckButton(dialog, "확인"); return Task.CompletedTask;
            });
            check(Input(Key.D1) == "⠂" && Input(Key.OemComma) == "⠄" && Input(Key.Q, Key.OemQuestion) == "⠡",
                "Custom digit, punctuation and multi-key chords type the assigned dots after applying the settings");
            await Open(dialog =>
            {
                check(dialog.UseCustomKeys && Physical(dialog) == ",1Q4-/", "Custom fields reopen at their physical keyboard positions");
                Modes(dialog)[0].IsChecked = true; CheckButton(dialog, "취소"); return Task.CompletedTask;
            });
            check(Input(Key.D1) == "⠂", "Canceling a preset switch leaves the previous custom mapping active in the editor");

            var workspace = new LocalWorkspace(Path.Combine(directoryPath, "workspace.json"));
            var accounts = new AccountPreferenceStore(directoryPath);
            const string legacy = "W E R U I O";
            await workspace.SaveAsync(new EditorPreferences(SixDotKeys: legacy) { UseCustomSixDotKeys = true });
            await accounts.SaveAsync("six-dot-check", new AccountPreferences(SixDotKeys: legacy) { UseCustomSixDotKeys = true });
            check(workspace.Load() is { UseCustomSixDotKeys: true, SixDotKeys: legacy } && accounts.Load("six-dot-check") is { UseCustomSixDotKeys: true, SixDotKeys: legacy },
                "Both preference stores preserve a custom choice even when it resembles an old preset");
            await File.WriteAllTextAsync(Path.Combine(directoryPath, "workspace.json"), """{"sixDotKeys":"W E R U I O"}""");
            check(workspace.Load() is { UseCustomSixDotKeys: false, SixDotKeys: "R E W U I O" }, "An existing preferences file without a mode still migrates the legacy preset");

            string? Input(params Key[] keys)
            {
                if (!grid.GoToFace(1)) throw new InvalidOperationException("Missing six-dot test face");
                inserted = null;
                var source = PresentationSource.FromVisual(grid) ?? throw new InvalidOperationException("Grid has no presentation source");
                foreach (var k in keys) grid.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, k) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                foreach (var k in keys.Reverse()) grid.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, k) { RoutedEvent = Keyboard.PreviewKeyUpEvent });
                return inserted;
            }
        }
        finally { editor.Session.IsDirty = false; editor.Close(); }

        async Task Open(Func<SixDotDialog, Task> inspect)
        {
            var interaction = editor.Dispatcher.InvokeAsync(async () =>
            {
                var dialog = Application.Current.Windows.OfType<SixDotDialog>().Single(w => w.Owner == editor);
                try { await inspect(dialog); }
                finally { if (dialog.IsVisible) dialog.Close(); }
            }, DispatcherPriority.ApplicationIdle);
            await editor.ExecuteAsync("sixdot"); await interaction.Task.Unwrap();
        }

        void CheckFrame(SixDotDialog dialog, string physical, bool custom)
        {
            var cards = Tree(dialog).OfType<Border>().Where(b => b.Child is StackPanel && b.CornerRadius.TopLeft == 8)
                .Select(b => b.TransformToAncestor(dialog).TransformBounds(new Rect(b.RenderSize))).ToArray();
            var fields = Tree(dialog).OfType<TextBox>().ToArray();
            check(dialog.ActualWidth == 640 && dialog.ActualHeight == 326 && cards.SequenceEqual([new Rect(25, 85, 590, 44), new Rect(25, 173, 590, 80)]),
                $"Six-dot {physical} Figma size and both card bounds");
            check(Physical(dialog) == physical && fields.Select(f => AutomationProperties.GetName(f)).SequenceEqual(new[] { "3점 키", "2점 키", "1점 키", "4점 키", "5점 키", "6점 키" })
                && fields.Select(f => f.TranslatePoint(new Point(), dialog).X).SequenceEqual(new double[] { 41, 89, 137, 213, 261, 309 })
                && fields.All(f => f.TranslatePoint(new Point(), dialog).Y == 209 && f.ActualWidth == 44 && f.ActualHeight == 32),
                $"Six-dot {physical} physical positions, tab sequence and accessible dot names agree");
            var close = Tree(dialog).OfType<SvgViewbox>().Single();
            check(close.Source.ToString().EndsWith("table-cancel.svg") && close.Width == 16 && close.Height == 16 && close.Drawings is { Bounds.IsEmpty: false }
                && fields.All(f => f.IsReadOnly != custom && f.Focusable == custom && f.Background.ToString() == (custom ? "#FFFFFFFF" : "#FFF5F5F5")),
                $"Six-dot {physical} close glyph and editable/read-only field presentation");
        }
    }

    private static RadioButton[] Modes(Window w) => Tree(w).OfType<RadioButton>().ToArray();
    private static string Physical(Window w) => string.Concat(Tree(w).OfType<TextBox>().OrderBy(f => f.TranslatePoint(new Point(), w).X).Select(f => f.Text));
    private static void CheckButton(Window w, string label) => Tree(w).OfType<Button>().Single(b => Equals(b.Content, label)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static IEnumerable<DependencyObject> Tree(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in Tree(child)) yield return nested;
        }
    }
}
