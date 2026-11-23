using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Threading;
using Semojum.Desktop.Dialogs;
using SharpVectors.Converters;

namespace Semojum.Desktop.Services;

internal static class WindowsInstitutionVisualChecks
{
    internal static async Task RunAsync(Action<bool, string> check)
    {
        // The cumulative usedCredits (3,120) differs from this month's 1,140 so the detail shows which one it uses.
        var account = new InstitutionAccount("kblib02", "수학 담당", "ACTIVE", "ROLE_USER", "2026-08-13", 3120, false, "math@kblib.or.kr", 2000, 1140, "2026-02-24T10:00:00");
        var jobs = new InstitutionAccountJobs("kblib02", "수학 담당", "2026-08-01", "2026-08-31",
            [new("job", "문서.pdf", "IMAGE", "DONE", 14, 14, 0, 11, null, null), new("job2", "둘째.pdf", "IMAGE", "DONE", 28, 28, 0, null, null, null)], 42, 11);
        var notice = new InstitutionNotice(Guid.NewGuid(), "ORG", "크레딧 소진 임박 안내", "운영자가 보낸 실제 공지 내용입니다.", null, null, "2026-08-13", "MAINTENANCE");
        var order = new InstitutionOrder(Guid.NewGuid(), "2026-06-02", "크레딧 추가 · 3,000", 780000, 3000, null, "PENDING", null, "kblib00", "ROLE_ORG_ADMIN", "2026-06-02T02:20:00Z", "견적서_20260602.pdf");
        var views = new (Window Window, string Name, double Width, double Height, string Asset)[]
        {
            (new InstitutionRequestDialog(true, (_, _) => Task.CompletedTask), "institution-account-request.png", 640, 318, "institution-account-request-close.svg"),
            (new InstitutionRequestDialog(false, (_, _) => Task.CompletedTask), "institution-credit-request.png", 600, 250, "institution-credit-request-close.svg"),
            (new InstitutionAccountDetailDialog(account, jobs, _ => Task.CompletedTask, (_, _, _) => Task.CompletedTask), "institution-account-detail.png", 560, 462, "institution-account-detail-close.svg"),
            (new InstitutionNoticeDialog(notice, true), "institution-notice-detail.png", 560, 206, "institution-notice-detail-close.svg"),
            (new InstitutionOrderDialog(order, null, () => Task.CompletedTask), "institution-order-detail.png", 560, 382, "institution-order-detail-close.svg")
        };
        foreach (var (window, name, width, height, asset) in views)
        {
            window.Show();
            try
            {
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
                var icon = Descendants(window).OfType<SvgViewbox>().SingleOrDefault(i => i.Source?.ToString().EndsWith("/" + asset, StringComparison.Ordinal) == true);
                check(Math.Abs(window.Width - width) < .1 && Math.Abs(window.Height - height) < .1 && icon is { Width: 16, Height: 16, Drawings.Bounds.IsEmpty: false },
                    $"{name} has its Figma window dimensions and original close icon");
                Point At(FrameworkElement element) => element.TransformToAncestor(window).Transform(new Point());
                bool AtPoint(FrameworkElement element, double x, double y) => Math.Abs(At(element).X - x) <= 1.5 && Math.Abs(At(element).Y - y) <= 1.5;
                var texts = Descendants(window).OfType<TextBlock>().Where(t => t.IsVisible).ToArray();
                if (window is InstitutionAccountDetailDialog detail)
                {
                    check(AtPoint(detail.AliasBox, 203, 69) && AtPoint(detail.EmailBox, 203, 113) && AtPoint(detail.LimitBox, 203, 157)
                        && detail.AliasBox.Text == account.Alias && detail.EmailBox.Text == account.Email && detail.LimitBox.Text == "2,000",
                        "F1 account details show editable server values in all three design rows");
                    check(texts.Any(t => t.Text == "활성") && texts.Any(t => t.Text == "2026-02-24") && texts.Any(t => t.Text == "1,140")
                        && AtPoint(detail.LockButton, 25, 413) && AtPoint(detail.SaveButton, 447, 413), "F1 account state and monthly usage render beside the designed lock and save actions");
                    check(!detail.Jobs.IsVisible && texts.Any(t => t.Text == "문서.pdf"), "F1 initially shows the latest job on one row");
                    detail.RecentJobs.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
                    check(detail.Jobs.IsVisible && detail.Jobs.Items.Count == 2 && detail.Jobs.Columns[0].GetCellContent(detail.Jobs.Items[0]) is TextBlock { Text: "문서.pdf" }
                        && detail.Jobs.Columns[1].GetCellContent(detail.Jobs.Items[0]) is TextBlock { Text: "14쪽" }
                        && detail.Jobs.Columns[2].GetCellContent(detail.Jobs.Items[1]) is TextBlock { Text: "— 크레딧" }, "Expanding the recent job retains both files, their page counts and unknown credit values");
                    detail.RecentJobs.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout();
                }
                else if (window is InstitutionOrderDialog orderDetail)
                {
                    check(texts.Any(t => t.Text == "주문 상세 · 06-02") && texts.Any(t => t.Text == "780,000원 (부가세 포함)") && texts.Any(t => t.Text == "미납")
                        && texts.Any(t => t.Text == "2026-06-02" && (string?)t.ToolTip == "kblib00 · 관리자 · 2026-06-02 11:20")
                        && orderDetail.QuoteButton.IsVisible && AtPoint(orderDetail.QuoteButton, 25, 333),
                        "F1 order detail shows the actual date, VAT-inclusive amount, recipient and original request metadata, with the quote on the left");
                }
                else if (window is InstitutionNoticeDialog noticeDetail)
                {
                    var paragraph = texts.Single(t => t.Text == notice.Body);
                    check(AtPoint(paragraph, 25, 93) && texts.Any(t => t.Text == "2026-08-13") && AtPoint(Descendants(window).OfType<Button>().Single(b => Equals(b.Content, "확인")), 447, 157)
                        && noticeDetail.ContextMenu.Items.OfType<MenuItem>().Any(i => i.IsEnabled && Equals(i.Header, "크레딧 추가 요청")), "F1 notice shows its date and unboxed message; confirmation and the credit request shortcut remain available");
                }
                else if (window is InstitutionRequestDialog requestBody)
                {
                    check(AtPoint(requestBody.First, 203, 69) && AtPoint(requestBody.Submit, width - 113, height - 49) && Equals(requestBody.Submit.Content, "요청"),
                        $"{name} positions the first control and submit button at the APP_UI coordinates");
                    if (name == "institution-credit-request.png")
                        check(requestBody.EstimateAmount is { IsVisible: true, Text: "—", FontSize: 14 } && !requestBody.EstimateNote.IsVisible,
                            "A missing server quote stays empty until a real amount arrives");
                }
                if (Environment.GetEnvironmentVariable("INSTITUTION_VISUAL_DIR") is { Length: > 0 } directory)
                { Directory.CreateDirectory(directory); await WindowsSmoke.RenderElementAsync(window, Path.Combine(directory, name)); }
                if (window is InstitutionRequestDialog request)
                {
                    if (name == "institution-credit-request.png")
                    {
                        var up = Descendants(window).OfType<Button>().Single(b => AutomationProperties.GetName(b) == "크레딧 1 증가");
                        var down = Descendants(window).OfType<Button>().Single(b => AutomationProperties.GetName(b) == "크레딧 1 감소");
                        var upIcon = up.Content as SvgViewbox; var downIcon = down.Content as SvgViewbox;
                        check(upIcon is { Width: 12, Height: 12, Drawings.Bounds.IsEmpty: false } && downIcon is { Width: 12, Height: 12, Drawings.Bounds.IsEmpty: false },
                            "Credit input renders the original Figma up and down spin icons at their supplied viewport sizes");
                        up.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); up.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); down.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        check(request.First.Text == "1", "Credit spin controls increment and decrement the real request quantity without going below one"); request.First.Clear();
                    }
                    var choices = request.ChoicesShown;
                    bool accountRequest = name == "institution-account-request.png";
                    check(choices.Count == 3 && choices[0].IsChecked == true && choices.All(r => r.IsVisible && r.ActualHeight == 20)
                        && AtPoint(choices[0], 203, accountRequest ? 157 : 145)
                        && (accountRequest ? AtPoint(choices[1], 203, 185) : Math.Abs(At(choices[1]).Y - 145) < 1.5),
                        $"{name} renders its three mutually exclusive options in the Figma orientation");
                    choices[1].IsChecked = true;
                    check(choices[0].IsChecked == false && choices[1].IsChecked == true, $"{name} lets the user select the second option");
                }
            }
            finally { window.Close(); }
        }
        foreach (var since in new string?[] { "2026-10-21T15:12:00Z", null })
        {
            var member = account with { LoginId = "kblib03", Alias = null, Status = "INACTIVE", MonthUsedCredits = 0, LockedAt = since };
            var locked = new InstitutionAccountDetailDialog(member, jobs with { Items = [] }, _ => Task.CompletedTask, (_, _, _) => Task.CompletedTask);
            locked.Show();
            try
            {
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); locked.UpdateLayout();
                var expected = since is null ? "잠김" : "잠김 · 10-22 부터";
                check(Equals(locked.LockButton.Content, "잠금 해제") && locked.LockButton.IsEnabled && !locked.RecentJobs.IsEnabled
                    && Descendants(locked).OfType<TextBlock>().Any(t => t.IsVisible && t.Text == expected),
                    since is null ? "A historical lock with no timestamp shows only its actual state" : "F1 locked account displays the start in Korea time and offers unlock");
                if (since is not null && Environment.GetEnvironmentVariable("INSTITUTION_VISUAL_DIR") is { Length: > 0 } folder)
                    await WindowsSmoke.RenderElementAsync(locked, Path.Combine(folder, "institution-account-locked.png"));
            }
            finally { locked.Close(); }
        }
        // An older order (no requester, no quote) keeps the order date and hides the quote button.
        var older = new InstitutionOrderDialog(new(Guid.NewGuid(), "2026-02-24", "연간 계약 · 10,000 크레딧", 2400000, 10000, "2026-02-25", "ISSUED", "계산서.pdf"), "account@kblib.or.kr", () => Task.CompletedTask);
        older.Show();
        try
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); older.UpdateLayout();
            var texts = Descendants(older).OfType<TextBlock>().Where(t => t.IsVisible).Select(t => t.Text).ToArray();
            check(older.QuoteButton.Visibility == Visibility.Collapsed && texts.Contains("주문 상세 · 02-24") && texts.Contains("2026-02-24")
                && texts.Contains("완납") && texts.Contains("발행 완료") && texts.Contains("account@kblib.or.kr"),
                "An order without a quote hides 견적서 내려받기, and an order without requester fields keeps its order date");
        }
        finally { older.Close(); }
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    { yield return root; for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) foreach (var child in Descendants(VisualTreeHelper.GetChild(root, i))) yield return child; }
}
