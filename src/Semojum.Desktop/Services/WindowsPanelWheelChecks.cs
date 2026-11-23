using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Semojum.Core;

namespace Semojum.Desktop.Services;

internal static class WindowsPanelWheelChecks
{
    internal static async Task RunAsync(Action<bool, string> check)
    {
        var window = new MainWindow(smoke: true, testWithoutAccount: true, startEmpty: true) { ShowInTaskbar = false };
        window.Show();
        try
        {
            // This suite isolates wheel routing; asynchronous translation is covered separately.
            typeof(MainWindow).GetField("autoTranslate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(window, false);
            var model = window.Session;
            model.Blocks.Clear(); model.ActiveBlock = null;
            var first = new EditorBlock { Id = 1, Page = 1, Source = string.Join("\n", Enumerable.Repeat("첫 페이지", 100)), Braille = "⠁" };
            var second = new EditorBlock { Id = 2, Page = 2, Source = "둘째 페이지", Braille = "⠃" };
            model.Add(first); model.Add(second); model.ActiveBlock = first;
            var editor = window.PageEditor.Editor;
            await Settle();
            var scroll = Descendants(editor).OfType<ScrollViewer>().First();
            scroll.ScrollToVerticalOffset(100); window.UpdateLayout();
            var middle = Wheel(-120); await Settle();
            check(!middle.Handled && model.ActiveBlock == first, "QA1110: source wheel inside a page preserves ordinary scrolling");
            scroll.ScrollToBottom(); window.UpdateLayout();
            await WaitForWheelEdge(bottom: true);
            var next = Wheel(-120);
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (window.PageEditor.DisplayedPage != 2 && DateTime.UtcNow < deadline) await Task.Delay(20);
            await Settle();
            check(next.Handled && window.PageEditor.DisplayedPage == 2 && model.ActiveBlock == first && window.DisplayedOriginalPage == 1 && scroll.VerticalOffset < 1,
                $"QA1110: source wheel at bottom opens next page at top (handled={next.Handled}, page={model.ActiveBlock?.Page}, offset={scroll.VerticalOffset}, extent={scroll.ScrollableHeight}, status={model.Status})");
            var previous = Wheel(120); await Settle();
            check(previous.Handled && window.PageEditor.DisplayedPage == 1 && model.ActiveBlock == first && scroll.VerticalOffset >= scroll.ScrollableHeight - 1,
                "QA1110: source wheel at top opens previous page at bottom");
            scroll.ScrollToTop(); window.UpdateLayout();
            await WaitForWheelEdge(bottom: false);
            var boundary = Wheel(120); await Settle();
            check(!boundary.Handled && model.ActiveBlock == first, "QA1110: source wheel respects first-page boundary");

            model.ActiveBlock = second; await Settle();
            var end = Wheel(-120); await Settle();
            check(!end.Handled && model.ActiveBlock == second, "QA1110: source wheel respects last-page boundary");
            var grid = window.Braille.GridView;
            first.Braille = string.Join("\n", Enumerable.Repeat("⠁", 100));
            await Settle();
            var brailleScroll = Descendants(grid).OfType<ScrollViewer>().First();
            brailleScroll.ScrollToTop(); window.UpdateLayout();
            void BrailleWheel(int delta) => brailleScroll.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta)
                { RoutedEvent = Mouse.MouseWheelEvent, Source = brailleScroll });
            BrailleWheel(-120); await Settle();
            var step = brailleScroll.VerticalOffset;
            check(step > 0 && step < brailleScroll.ViewportHeight,
                $"QA1110: one braille wheel notch moves less than one viewport (step={step}, viewport={brailleScroll.ViewportHeight})");
            var start = brailleScroll.VerticalOffset;
            for (var i = 0; i < 20; i++) { BrailleWheel(-120); window.UpdateLayout(); }
            await Settle();
            check(brailleScroll.VerticalOffset > start + 26 * 28,
                "QA1110: repeated braille wheel input crosses physical face boundaries");
            var forward = brailleScroll.VerticalOffset;
            BrailleWheel(120); await Settle();
            check(brailleScroll.VerticalOffset < forward, "QA1110: braille wheel can scroll back toward previous faces");
            // Re-select the active block after each neighbouring panel has browsed away.
            model.ActiveBlock = first;
            window.PageEditor.BrowsePage(2);
            grid.GoToFace(grid.Items.Count);
            await Settle();
            check(window.DisplayedOriginalPage == 1 && window.PageEditor.DisplayedPage == 2
                && model.ActiveBlock == first && window.Braille.DisplayedFace == grid.Items.Count,
                "Each panel can display a different location without selecting a new block");

            editor.Focus();
            editor.TextArea.TextView.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
            editor.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                { RoutedEvent = Mouse.MouseUpEvent });
            await Settle();
            check(model.ActiveBlock == second && window.DisplayedOriginalPage == 2 && grid.ActiveBlock == second,
                "Clicking browsed source text selects its matching original page and braille block");

            window.PageEditor.BrowsePage(1);
            grid.Point(new(second, 0, '⠃'), false);
            await Settle();
            check(model.ActiveBlock == second && window.PageEditor.DisplayedPage == 2 && window.DisplayedOriginalPage == 2,
                "Clicking the same braille block again brings the independently browsed source back");

            window.PageEditor.BrowsePage(1);
            grid.GoToFace(1);
            await window.SelectOriginalRegionAsync(second);
            await Settle();
            check(window.PageEditor.DisplayedPage == 2 && grid.ActiveBlock == second && window.Braille.DisplayedFace == grid.Items.Count,
                "Clicking the same original block again brings both browsed panels back");

            async Task WaitForWheelEdge(bool bottom)
            {
                // ScrollViewer queues its scroll command. Wait for the AvalonEdit view that handles the wheel.
                var view = (System.Windows.Controls.Primitives.IScrollInfo)editor.TextArea.TextView;
                var limit = DateTime.UtcNow.AddSeconds(3);
                do
                {
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    var edge = bottom ? Math.Max(0, view.ExtentHeight - view.ViewportHeight) : 0;
                    if (Math.Abs(view.VerticalOffset - edge) <= .5 && Keyboard.Modifiers == ModifierKeys.None) return;
                    await Task.Delay(20);
                } while (DateTime.UtcNow < limit);
                throw new InvalidOperationException($"Wheel fixture did not reach the requested edge (offset={view.VerticalOffset}, height={view.ExtentHeight}, viewport={view.ViewportHeight}, modifiers={Keyboard.Modifiers})");
            }
            MouseWheelEventArgs Wheel(int delta)
            {
                var args = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta)
                { RoutedEvent = Mouse.PreviewMouseWheelEvent, Source = editor };
                editor.RaiseEvent(args); return args;
            }
            async Task Settle()
            {
                await Task.Delay(150); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
            }
        }
        finally { window.PrepareSmokeShutdown(); window.Close(); }
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
