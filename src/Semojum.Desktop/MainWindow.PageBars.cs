using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Semojum.Core;
using Semojum.Core.Integration;
using Semojum.Desktop.Controls;

namespace Semojum.Desktop;

public partial class MainWindow
{
    internal int DisplayedOriginalPage { get; private set; }
    private bool sourceWheelBusy;
    private async void SourceEditor_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || e.Delta == 0 || Keyboard.Modifiers != ModifierKeys.None || session?.ActiveBlock is null) return;
        if (sourceWheelBusy) { e.Handled = true; return; }
        var view = (System.Windows.Controls.Primitives.IScrollInfo)PageEditor.Editor.TextArea.TextView;
        var backwards = e.Delta > 0;
        if (backwards ? view.VerticalOffset > .5 : view.VerticalOffset < view.ExtentHeight - view.ViewportHeight - .5) return;
        var owner = session;
        var pages = owner.Blocks.Select(b => b.Page).Concat(owner.PageStatuses.Keys).Distinct().Order().ToArray();
        var index = Array.IndexOf(pages, PageEditor.DisplayedPage) + (backwards ? -1 : 1);
        if (index < 0 || index >= pages.Length) return;
        var page = pages[index];
        var target = owner.Blocks.FirstOrDefault(b => b.Page == page);
        if (target is null || target.Metadata.IsBlocked ||
            owner.PageStatuses.TryGetValue(page, out var status) && JobStatus.IsFailedPage(status)) return;
        e.Handled = true;
        sourceWheelBusy = true;
        try
        {
            BrowseSourcePage(page);
            await Dispatcher.InvokeAsync(() =>
            {
                if (session != owner || PageEditor.DisplayedPage != page) return;
                PageEditor.Editor.UpdateLayout();
                PageEditor.Editor.ScrollToVerticalOffset(backwards ? double.MaxValue : 0);
            }, DispatcherPriority.Loaded);
        }
        catch (Exception ex) { if (session == owner) owner.Status = ex.Message; }
        finally { sourceWheelBusy = false; }
    }
    private (DocumentSession Owner, int Page, bool Bottom)? originalWheelDestination;

    private void OriginalScroll_MouseWheel(object sender, MouseWheelEventArgs e) => HandleOriginalWheel((ScrollViewer)sender, e);
    private void OriginalPreview_MouseWheel(object sender, MouseWheelEventArgs e) => HandleOriginalWheel(Original.PreviewScroll, e);

    private void HandleOriginalWheel(ScrollViewer scroll, MouseWheelEventArgs e)
    {
        if (originalWheelDestination is { } stale && (stale.Owner != session || DisplayedOriginalPage != stale.Page))
            originalWheelDestination = null;
        if (e.Handled || e.Delta == 0 || Keyboard.Modifiers != ModifierKeys.None ||
            originalWheelDestination is not null ||
            session?.IsTextJob == true || session?.ActiveBlock is null ||
            !HasReadableOriginal(session, DisplayedOriginalPage)) return;

        // Preserve normal scrolling inside a page. Only leave it when the wheel
        // points past the existing top or bottom edge.
        var backwards = e.Delta > 0;
        if (backwards ? scroll.VerticalOffset > .5 : scroll.VerticalOffset < scroll.ScrollableHeight - .5) return;
        var pages = session.OriginalPages.Keys.Concat(session.PageStatuses.Keys).Distinct().Order().ToArray();
        var index = Array.IndexOf(pages, DisplayedOriginalPage) + (backwards ? -1 : 1);
        if (index < 0 || index >= pages.Length) return;
        var page = pages[index];
        var target = session.Blocks.FirstOrDefault(b => b.Page == page);
        if (target is null || target.Metadata.IsBlocked || !HasReadableOriginal(session, page) ||
            session.PageStatuses.TryGetValue(page, out var status) && JobStatus.IsFailedPage(status)) return;

        e.Handled = true;
        var owner = session;
        originalWheelDestination = (owner, page, backwards);
        BrowseOriginalPage(page);
        if (smoke) FinishOriginalWheelTransition(owner, page, scroll);
    }

    private static bool HasReadableOriginal(DocumentSession owner, int page)
    {
        if (!owner.OriginalPages.TryGetValue(page, out var original)) return false;
        return original.Type == "text" ? original.Lines is not null :
            original.AssetName is { Length: > 0 } name && owner.OriginalFiles.ContainsKey(name);
    }

    private void FinishOriginalWheelTransition(DocumentSession owner, int page, ScrollViewer scroll)
    {
        if (originalWheelDestination is not { } pending || pending.Owner != owner || pending.Page != page) return;
        originalWheelDestination = null;
        // Wait for the new image/text to be measured before moving to its edge.
        Dispatcher.BeginInvoke(() =>
        {
            if (session != owner || DisplayedOriginalPage != page) return;
            scroll.UpdateLayout();
            if (pending.Bottom) scroll.ScrollToBottom(); else scroll.ScrollToTop();
        }, DispatcherPriority.Loaded);
    }
    private void OriginalPageBar_Loaded(object sender, RoutedEventArgs e)
    {
        OriginalPageBar.SetIcons("panel-original-prev.svg", "panel-original-next.svg");
        OriginalPageBar.PageRequested -= BrowseOriginalPage;
        OriginalPageBar.PageRequested += BrowseOriginalPage;
        OriginalPageBar.InvalidPageRequested -= InvalidPage;
        OriginalPageBar.InvalidPageRequested += InvalidPage;
        RefreshPanelPageBars();
    }
    private void SourcePageBar_Loaded(object sender, RoutedEventArgs e)
    {
        SourcePageBar.SetIcons("panel-page-prev.svg", "panel-page-next.svg");
        SourcePageBar.PageRequested -= BrowseSourcePage;
        SourcePageBar.PageRequested += BrowseSourcePage;
        SourcePageBar.InvalidPageRequested -= InvalidPage;
        SourcePageBar.InvalidPageRequested += InvalidPage;
        RefreshPanelPageBars();
    }
    private void RefreshPanelPageBars()
    {
        if (session is null || OriginalPageBar is null || SourcePageBar is null || OriginalUpload is null) return;
        var editable = session.Blocks.Select(b => b.Page).Distinct().Order().ToArray();
        var all = editable.Concat(session.OriginalPages.Keys).Concat(session.PageStatuses.Keys).Distinct().Order().ToArray();
        var original = session.OriginalPages.Keys.Concat(session.PageStatuses.Keys).Distinct().Order().ToArray();

        var failedPages = session.PageStatuses.Keys.Where(session.IsConversionFailed).ToArray();
        OriginalPageBar.SetPages(original, editable.Intersect(original).Union(failedPages), DisplayedOriginalPage, session.TotalOriginalPages, inferTotal: false);
        SourcePageBar.SetPages(all, editable.Union(failedPages), PageEditor.DisplayedPage, session.TotalOriginalPages, inferTotal: session.JobId is null || session.IsTextJob == true);
    }
    private void BrowseOriginalPage(int page)
    {
        ClearOriginalRegion();
        originalRevealTarget = null;
        Original.SelectBlock(0, page);
        ShowOriginal(page);
        RefreshEditorStatus();
    }

    private void BrowseSourcePage(int page)
    {
        ClearOriginalRegion();
        PageEditor.BrowsePage(page);
        RefreshEditorStatus();
    }

    private void InvalidPage() => session.Status = "편집할 수 있는 쪽 번호를 입력해 주세요. 실패한 쪽은 쪽별 변환 상태에서 확인할 수 있습니다.";
}
