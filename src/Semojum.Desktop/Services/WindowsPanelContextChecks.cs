using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Semojum.Core;

namespace Semojum.Desktop.Services;

internal static class WindowsPanelContextChecks
{
    internal static async Task RunAsync(string directory, Action<bool, string> check)
    {
        var window = new MainWindow(smoke: true, testWithoutAccount: true, startEmpty: true);
        window.Show();
        try
        {
            window.AdditionalCommands.IsChecked = false;
            var block = window.Session.ActiveBlock!;
            block.Source = "그림 설명";
            block.Metadata = new(Type: "image", Alternatives: [new("개조식", "설명", null, "⠃", null)]);
            block.Translation = BrailleTranslation.Plain("⠁") with { SourceText = block.Source };
            block.Braille = "⠃"; block.HasManualBraille = true;
            var shots = Directory.CreateDirectory(Path.Combine(directory, "panel-menus")).FullName;
            foreach (var (state, target, groupTitle, mainHeight, nestedHeight) in new (string, FrameworkElement, string?, int, int)[] {
                ("text", window.PageEditor.Editor, "강조 넣기", 366, 138),
                ("braille", window.Braille.GridView, "점자 줄 넣기", 293, 170),
                ("original", window.OriginalPanel, null, 42, 0) })
            {
                var menu = target.ContextMenu ?? throw new InvalidOperationException("Missing context menu: " + state);
                menu.PlacementTarget = target; menu.Placement = PlacementMode.RelativePoint;
                var anchor = window.TranslatePoint(new Point(50, 180), target);
                menu.HorizontalOffset = anchor.X; menu.VerticalOffset = anchor.Y;
                menu.IsOpen = true;
                try
                {
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); menu.UpdateLayout();
                    check(menu.IsVisible && menu.ActualWidth == 248 && menu.ActualHeight == mainHeight,
                        $"{state} context menu opens at 248×{mainHeight} (actual {menu.RenderSize})");
                    var items = menu.Items.OfType<MenuItem>().Where(m => m.IsVisible).ToArray();
                    if (groupTitle is null)
                    {
                        check(items is [{ Header: "재변환", Tag: "reconvert", Icon: not null }], "The original panel offers the actual reconversion command");
                        await WindowsSmoke.RenderElementAsync(menu, Path.Combine(shots, state + ".png"));
                        continue;
                    }
                    var group = items.Single(m => Equals(m.Header, groupTitle));
                    group.IsSubmenuOpen = true;
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); group.UpdateLayout();
                    var popup = (Popup)group.Template.FindName("PART_Popup", group);
                    var surface = (Border)popup.Child;
                    surface.UpdateLayout();
                    var at = menu.PointFromScreen(surface.PointToScreen(new Point()));
                    check(popup.IsOpen && surface.ActualWidth == 200 && surface.ActualHeight == nestedHeight &&
                        Math.Abs(at.X - 248) < 1.1 && Math.Abs(at.Y - 219) < 1.1,
                        $"{state} submenu opens beside its row at 248,219 with size 200×{nestedHeight} (actual {at}, {surface.RenderSize})");
                    if (state == "text")
                        check(items.Last() is { InputGestureText: "Alt+M", Tag: "math" } &&
                            window.InputBindings.OfType<KeyBinding>().Any(b => b.Key == Key.M && b.Modifiers == ModifierKeys.Alt && b.Command is not null),
                            "The displayed Alt+M shortcut is bound to the editor's math action");
                    else
                        check(group.Items.OfType<MenuItem>().Select(m => m.Header).SequenceEqual(new[] { "상단","중단","하단","상단 굵은","하단 굵은" })
                            && items.Single(m => Equals(m.Header, "직접 고친 점자 되돌리기")).IsEnabled
                            && !items.Any(m => Equals(m.Tag, "translate")), "The regular braille menu shows the five line styles and can restore this document's manual braille");
                    RenderPair(menu, surface, at, Path.Combine(shots, state + ".png"));
                    group.IsSubmenuOpen = false;
                    if (state == "braille")
                    {
                        window.AdditionalCommands.IsChecked = true;
                        menu.UpdateLayout();
                        check(menu.Items.OfType<MenuItem>().Single(m => Equals(m.Tag, "translate")).IsVisible,
                            "The full commands setting makes per-block retranslation available in the braille context menu");
                        window.AdditionalCommands.IsChecked = false;
                    }
                }
                finally { menu.IsOpen = false; }
            }
        }
        finally { await window.ReleaseResourcesAsync(); window.PrepareSmokeShutdown(); window.Close(); }
    }

    // A WPF submenu has its own HWND. Use its measured screen displacement to include both live surfaces.
    private static void RenderPair(ContextMenu menu, Border submenu, Point origin, string file)
    {
        var root = new Rect(new Point(), menu.RenderSize);
        // Round monitor coordinates to the output bitmap’s 96-DPI pixel grid.
        var child = new Rect(new Point(Math.Round(origin.X), Math.Round(origin.Y)), submenu.RenderSize);
        var bounds = Rect.Union(root, child);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.PushTransform(new TranslateTransform(-bounds.Left, -bounds.Top));
            context.DrawRectangle(new VisualBrush(menu), null, root);
            context.DrawRectangle(new VisualBrush(submenu), null, child);
            context.Pop();
        }
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(bounds.Width), (int)Math.Ceiling(bounds.Height), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(file); encoder.Save(stream);
    }
}
