using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Semojum.Desktop.Services;

internal static class WindowsResponsiveChecks
{
    internal static async Task RunAsync(Action<bool, string> check)
    {
        var window = new MainWindow(smoke: true, testWithoutAccount: true, startEmpty: true);
        window.AdditionalCommands.IsChecked = true; window.Show();
        try
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var work = SystemParameters.WorkArea;
            check(window.MinWidth >= Math.Min(1280, work.Width) - 1 && window.MinHeight >= Math.Min(800, work.Height) - 1 && window.ResizeMode == ResizeMode.CanResize,
                "Editor keeps the final 1280 by 800 DIP minimum (or the whole work area on a smaller monitor)");
            window.WindowState = WindowState.Normal; window.Width = 1280; window.Height = 800; window.UpdateLayout();
            check(window.ActualWidth <= 1281 && window.ActualHeight <= 801, "Editor accepts a 1280 by 800 DIP window");
            check(InBounds(window, window.OriginalPanel) && InBounds(window, window.SourcePanel) && InBounds(window, window.BrailleContainer),
                "All three panels fit in a 1280 DIP viewport");
            var before = window.Session.ActiveBlock!.Source;
            window.PageEditor.Editor.Document.Insert(0, "가");
            check(window.Session.ActiveBlock.Source == "가" + before, "The source editor remains usable in a 1280 DIP viewport");
            window.Width = 480; window.Height = 360; window.UpdateLayout();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            check(window.ActualWidth >= window.MinWidth - 1 && window.ActualHeight >= window.MinHeight - 1, "Editor does not shrink below its minimum window size");
            var panelScroll = window.FindName("PanelScroll") as ScrollViewer ?? throw new InvalidOperationException("PanelScroll missing");
            var toolScroll = window.FindName("EditorToolbarScroll") as ScrollViewer ?? throw new InvalidOperationException("EditorToolbarScroll missing");
            var formatScroll = window.FindName("FormatToolbarScroll") as ScrollViewer ?? throw new InvalidOperationException("FormatToolbarScroll missing");
            check(panelScroll is { ScrollableWidth: 0 } && toolScroll is { ScrollableWidth: 0 } && formatScroll is { ScrollableWidth: 0 },
                "Panels and both toolbars need no horizontal scrolling at the minimum window size");
            // WindowBounds.Fit lowers the minimum to a smaller monitor's work area; narrow viewports must stay reachable there.
            window.MinWidth = 480; window.MinHeight = 360; window.Width = 480; window.Height = 360; window.UpdateLayout();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            check(panelScroll is { ScrollableWidth: > 0 } && toolScroll is { ScrollableWidth: > 0 } && formatScroll is { ScrollableWidth: > 0 },
                "Panels and both toolbars offer horizontal scrolling in a narrow viewport");
            check(InBounds(window, window.ApplicationMenu), "The main application menu stays reachable in a narrow viewport");
            async Task ScrollEdge(ScrollViewer scroll, bool right)
            {
                // A resize can still have a queued request to bring the previous editor caret into view.
                // Let the new size settle, then wait until the requested scroll edge is actually visible.
                await Task.Delay(50);
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                for (var attempt = 0; attempt < 20; attempt++)
                {
                    if (right) scroll.ScrollToRightEnd(); else scroll.ScrollToLeftEnd();
                    window.UpdateLayout();
                    await Task.Delay(20);
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    if (Math.Abs(scroll.HorizontalOffset - (right ? scroll.ScrollableWidth : 0)) < 1) return;
                }
            }
            await ScrollEdge(panelScroll, false);
            check(InViewport(panelScroll, window.OriginalHeader), $"The original panel controls can be reached by scrolling left (header={window.OriginalHeader.RenderSize}, offset={panelScroll.HorizontalOffset}, viewport={panelScroll.ViewportWidth}×{panelScroll.ViewportHeight})");
            await ScrollEdge(panelScroll, true);
            check(InViewport(panelScroll, window.Braille.Header), $"The braille panel controls can be reached by scrolling right (header={window.Braille.Header.RenderSize}, offset={panelScroll.HorizontalOffset}, viewport={panelScroll.ViewportWidth}×{panelScroll.ViewportHeight})");
            await ScrollEdge(toolScroll, false);
            var save = Descendants(window.EditorToolbar).OfType<Button>().Single(b => Equals(b.Tag, "save"));
            check(InViewport(toolScroll, save), "The save toolbar command is reachable at the left edge");
            await ScrollEdge(toolScroll, true);
            check(InBounds(window, window.RuleSearch), "The rule search remains reachable in the ribbon header");
            await ScrollEdge(formatScroll, true);
            var lineNumbers = Descendants(formatScroll).OfType<Button>().Single(b => Equals(b.Tag, "lineNumbers"));
            check(InViewport(formatScroll, lineNumbers), "The final formatting command is reachable at the right edge");
            check(double.IsPositiveInfinity(window.MaxWidth) && double.IsPositiveInfinity(window.MaxHeight), "The editor is not permanently capped to its startup monitor size");
        }
        finally { await window.ReleaseResourcesAsync(); window.PrepareSmokeShutdown(); window.Close(); }
    }
    private static bool InBounds(Window window, FrameworkElement element)
    {
        if (!element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0) return false;
        var rect = element.TransformToAncestor(window).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        return rect.Left >= -1 && rect.Top >= -1 && rect.Right <= window.ActualWidth + 1 && rect.Bottom <= window.ActualHeight + 1;
    }
    private static bool InViewport(ScrollViewer scroll, FrameworkElement element)
    {
        if (!element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0) return false;
        var presenter = Descendants(scroll).OfType<ScrollContentPresenter>().FirstOrDefault(p => ReferenceEquals(p.TemplatedParent, scroll));
        if (presenter is null) return false;
        var rect = element.TransformToAncestor(presenter).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        return rect.Left >= -1 && rect.Top >= -1 && rect.Right <= presenter.ActualWidth + 1 && rect.Bottom <= presenter.ActualHeight + 1;
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        { var child = VisualTreeHelper.GetChild(parent, i); yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
}
