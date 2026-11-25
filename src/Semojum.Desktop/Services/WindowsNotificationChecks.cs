using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using Semojum.Core.Integration;
using Semojum.Desktop.Dialogs;

namespace Semojum.Desktop.Services;

internal static class WindowsNotificationChecks
{
    internal static async Task RunAsync(Action<bool, string> check, string directory)
    {
        var fixture = new Fixture(); using var http = new HttpClient(fixture); var api = new SemojumApiClient(http);
        await api.LoginAsync("fixture", "fixture"); var board = new BoardDialog(new BoardClient(api), ""); board.Show();
        try
        {
            await WaitAsync(() => fixture.BoardLists > 0);
            var entry = Descendants(board).OfType<Button>().FirstOrDefault(b => System.Windows.Automation.AutomationProperties.GetName(b) == "알림" && b.IsVisible);
            check(entry is not null,
                "The board provides a visible entry to notifications");
            var opened = false; var close = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
            close.Tick += (_, _) =>
            {
                var dialog = Application.Current.Windows.OfType<NotificationsDialog>().FirstOrDefault(d => d.Owner == board && d.IsVisible && d.IsEnabled);
                if (dialog is not null) { opened = true; dialog.Close(); }
            };
            close.Start(); try { entry!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); } finally { close.Stop(); }
            check(opened, "The board notification entry opens the notification list");
        }
        finally { board.Close(); }

        var notifications = new NotificationsDialog(new BoardClient(api), ""); notifications.Show();
        try
        {
            await WaitAsync(() => fixture.NotificationLists > 0);
            check(Descendants(notifications).OfType<TextBlock>().Any(t => t.IsVisible && t.Text.Contains("알림이 없습니다")),
                "An empty notification list explains that there are no notifications");
            check(fixture.AuthenticatedNotifications, "Notifications use the signed-in account");
        }
        finally { notifications.Close(); }

        var notices = new PublicNoticesDialog(api); notices.Show();
        try
        {
            await WaitAsync(() => fixture.PublicLists > 0);
            check(Descendants(notices).OfType<TextBlock>().Any(t => t.Text.Contains("현재 표시할 공지가 없습니다")),
                "Public notices explain the empty state");
            check(fixture.PublicWasAnonymous, "Public notices do not send account credentials");
        }
        finally { notices.Close(); }

        fixture.HoldPublic = true; var publicBefore = fixture.PublicLists;
        var slowPublic = new PublicNoticesDialog(api); slowPublic.Show();
        try
        {
            await WaitAsync(() => fixture.PublicLists > publicBefore);
            slowPublic.Close();
            check(!slowPublic.IsVisible, "A delayed public notice request does not prevent closing the window");
        }
        finally { fixture.ReleasePublic(); await Task.Delay(50); if (slowPublic.IsVisible) slowPublic.Close(); fixture.HoldPublic = false; }

        fixture.DenyPublic = true; publicBefore = fixture.PublicLists;
        var retryPublic = new PublicNoticesDialog(api); retryPublic.Show();
        try
        {
            await WaitAsync(() => fixture.PublicLists > publicBefore);
            check(Descendants(retryPublic).OfType<TextBlock>().Any(t => t.IsVisible && t.Text.Contains("접근할 수 없습니다")),
                "Public notice load errors are visible");
            check(!Descendants(retryPublic).OfType<Button>().Any(b => Equals(b.Content, "새로고침")) && Descendants(retryPublic).OfType<Button>().Count(b => Equals(b.Content, "닫기")) == 1,
                "Public notices offer only the final close action");
            fixture.DenyPublic = false; fixture.PopulatedPublic = true; publicBefore = fixture.PublicLists;
            retryPublic.Close(); retryPublic = new PublicNoticesDialog(api); retryPublic.Show();
            await WaitAsync(() => fixture.PublicLists > publicBefore && Descendants(retryPublic).OfType<TextBlock>().Any(t => t.Text == "점검 공지"));
            check(Descendants(retryPublic).OfType<TextBlock>().Any(t => t.Text == "점검 공지") && fixture.PublicWasAnonymous,
                "Reopened public notices load without account credentials");
            var cards = Descendants(retryPublic).OfType<Expander>().ToArray();
            check(cards.Length == 3 && cards[0].IsExpanded && !cards[1].IsExpanded && !cards[2].IsExpanded,
                "Public notice entries display one expanded and another collapsed");
            check(Descendants(cards[0]).OfType<TextBlock>().Any(t => t.Text == "2026. 10. 25."),
                "The public notice displays its server-provided creation date");
            check(!Descendants(cards[2]).OfType<TextBlock>().Any(t => t.Text.Contains("2026.")),
                "A missing notice date is not invented");
            var capture = Environment.GetEnvironmentVariable("SEMOJUM_NOTICE_CAPTURE");
            if (!string.IsNullOrWhiteSpace(capture)) await WindowsSmoke.RenderElementAsync(retryPublic, capture);
            var peer = UIElementAutomationPeer.CreatePeerForElement(cards[1]) as ExpanderAutomationPeer;
            var provider = peer?.GetPattern(PatternInterface.ExpandCollapse) as IExpandCollapseProvider;
            check(provider is not null, "The notice can be expanded through the accessibility API");
            provider!.Expand();
            await retryPublic.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            check(cards[1].IsExpanded && cards[1].Content is FrameworkElement { IsVisible: true },
                "Expanding another notice exposes its full body");
            provider.Collapse();
            check(!cards[1].IsExpanded, "The expanded notice can be collapsed");
        }
        finally { retryPublic.Close(); fixture.PopulatedPublic = false; }

        var unsignedApi = new SemojumApiClient(http); var account = new AccountDialog(unsignedApi); account.Show();
        var sawNotices = false; var sawInquiry = false; var closePublic = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        closePublic.Tick += (_, _) =>
        {
            foreach (Window dialog in Application.Current.Windows.Cast<Window>().Where(w => w.Owner == account && w.IsVisible).ToArray())
            {
                if (dialog is PublicNoticesDialog) sawNotices = true;
                if (dialog is PublicInquiryDialog) sawInquiry = true;
                dialog.Close();
            }
        };
        closePublic.Start();
        try
        {
            var navigation = Descendants(account).OfType<Button>().Single(b => System.Windows.Automation.AutomationProperties.GetAutomationId(b) == "LoginNavigation");
            navigation.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var noticeItem = navigation.ContextMenu.Items.OfType<MenuItem>().Single(m => Equals(m.Header, "공지사항"));
            navigation.ContextMenu.IsOpen = false; noticeItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Descendants(account).OfType<Button>().Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "문의하기").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(sawNotices && sawInquiry && unsignedApi.Account is null && fixture.PublicWasAnonymous,
                "Login opens public notices and inquiry without creating a session");
        }
        finally { closePublic.Stop(); account.Close(); }
        var inquiry = new PublicInquiryDialog(unsignedApi); inquiry.Show();
        try
        {
            var email = Descendants(inquiry).OfType<TextBox>().Single(t => System.Windows.Automation.AutomationProperties.GetName(t) == "답 받을 주소");
            var message = Descendants(inquiry).OfType<TextBox>().Single(t => t.AcceptsReturn && t.MaxLength == 2000);
            var organization = Descendants(inquiry).OfType<TextBox>().Single(t => System.Windows.Automation.AutomationProperties.GetName(t) == "기관명");
            var title = Descendants(inquiry).OfType<TextBox>().Single(t => System.Windows.Automation.AutomationProperties.GetName(t) == "제목");
            var submit = Descendants(inquiry).OfType<Button>().Single(b => Equals(b.Content, "보내기"));
            Descendants(inquiry).OfType<CheckBox>().Single(c => System.Windows.Automation.AutomationProperties.GetAutomationId(c) == "PublicInquiryConsent").IsChecked = true;
            email.Text = "invalid"; message.Text = "작성한 문의"; organization.Text = "테스트 기관"; title.Text = "문의 제목";
            submit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(50);
            check(fixture.InquiryPosts == 0 && message.Text == "작성한 문의", "Invalid inquiry is not sent and keeps the typed message");
            email.Text = "user@example.test"; submit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitAsync(() => fixture.InquiryPosts == 1);
            check(fixture.InquiryWasAnonymous && message.Text == "작성한 문의" && organization.Text == "테스트 기관" && title.Text == "문의 제목"
                && Descendants(inquiry).OfType<TextBlock>().Any(t => t.IsVisible && t.Text.Contains("접근할 수 없습니다")),
                "A failed anonymous inquiry reports its error and retains all typed fields");
            check(fixture.Inquiry is { } sent && sent.GetValueOrDefault("message") == "작성한 문의" && sent.GetValueOrDefault("orgName") == "테스트 기관" && sent.GetValueOrDefault("title") == "문의 제목"
                && sent.GetValueOrDefault("type") == "ACCOUNT" && !sent.ContainsKey("name"),
                "The default category, organization and title reach the anonymous inquiry as their own fields without a sender name");
            message.Text = new string('가', 2001);
            submit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(50);
            check(fixture.InquiryPosts == 1 && message.Text.Length == 2001
                && Descendants(inquiry).OfType<TextBlock>().Any(t => t.IsVisible && t.Text.Contains("2,000자")),
                "The inquiry length limit rejects the request without losing fields");
            message.Text = "작성한 문의";
            var cancel = Descendants(inquiry).OfType<Button>().Single(b => Equals(b.Content, "취소"));
            var sawConfirm = AnswerInquiryClose(inquiry, 0, () => cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            check(sawConfirm && inquiry.IsVisible && organization.Text == "테스트 기관" && title.Text == "문의 제목" && message.Text == "작성한 문의",
                "Choosing to continue after cancelling retains all inquiry fields");
            email.Text = ""; organization.Text = ""; message.Text = "";
            sawConfirm = AnswerInquiryClose(inquiry, 0, inquiry.Close);
            check(sawConfirm && inquiry.IsVisible && title.Text == "문의 제목", "A title alone is protected when closing the inquiry");
            title.Text = ""; organization.Text = "테스트 기관";
            sawConfirm = AnswerInquiryClose(inquiry, 0, () => inquiry.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(inquiry), Environment.TickCount, Key.Escape)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent }));
            check(sawConfirm && inquiry.IsVisible && organization.Text == "테스트 기관", "Escape protects an inquiry containing only an organization");
            sawConfirm = AnswerInquiryClose(inquiry, 1, inquiry.Close);
            check(sawConfirm && !inquiry.IsVisible, "Confirming discard closes a drafted inquiry");
        }
        finally { if (inquiry.IsVisible) AnswerInquiryClose(inquiry, 1, inquiry.Close); }

        fixture.HoldNotifications = true; var before = fixture.NotificationLists;
        var slow = new NotificationsDialog(new BoardClient(api), ""); slow.Show();
        try
        {
            await WaitAsync(() => fixture.NotificationLists > before);
            slow.Close();
            check(!slow.IsVisible, "A delayed notification list does not prevent closing the window");
        }
        finally { fixture.Release(); await Task.Delay(50); if (slow.IsVisible) slow.Close(); }

        fixture.HoldNotifications = false; fixture.Populated = true;
        var listWindow = new NotificationsDialog(new BoardClient(api), ""); listWindow.Show();
        var closePost = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) }; var openedInBoard = false;
        closePost.Tick += (_, _) =>
        {
            foreach (var post in Application.Current.Windows.OfType<BoardDialog>().Where(p => p.Owner == listWindow && p.IsVisible).ToArray())
            { openedInBoard |= post.Title == "게시판 - 누리닷" && Descendants(post).OfType<PostView>().Any(v => v.IsVisible); post.Close(); }
        };
        closePost.Start();
        try
        {
            var list = Descendants(listWindow).OfType<ListBox>().Single();
            await WaitAsync(() => list.Items.Count == 2);
            list.SelectedIndex = 0;
            Button Open() => Descendants(listWindow).OfType<Button>().Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "첫 문의");
            fixture.DenyPost = true; Open().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitAsync(() => fixture.PostRequests == 1);
            check(list.Items[0] is BoardNotification { Read: false } && fixture.PostRequests == 1,
                "A forbidden post does not mark the notification as read");
            fixture.DenyPost = false; Open().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitAsync(() => fixture.PostRequests == 2 && list.Items.Count == 2 && list.Items[0] is BoardNotification { Read: true });
            check(list.Items[0] is BoardNotification { Read: true } && list.Items[1] is BoardNotification { Read: false },
                "Opening a permitted post refreshes only its server-confirmed read status");
            check(openedInBoard, "A notification opens its post in the body of a 「게시판 - 누리닷」 window");
            var readAll = Descendants(listWindow).OfType<Button>().Single(b => Equals(b.Content, "모두 읽음"));
            fixture.DenyReadAll = true; readAll.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitAsync(() => fixture.ReadAllRequests == 1);
            check(list.Items[1] is BoardNotification { Read: false } && Descendants(listWindow).OfType<TextBlock>().Any(t => t.IsVisible && t.Text.Contains("접근할 수 없습니다")),
                "A failed read-all request reports the error and does not invent a read state");
            fixture.DenyReadAll = false; readAll.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitAsync(() => fixture.ReadAllRequests == 2 && list.Items[1] is BoardNotification { Read: true });
            check(fixture.MutationAuthorized && list.Items.Cast<BoardNotification>().All(n => n.Read), "Successful authenticated read-all refreshes server-confirmed statuses");
            check(Descendants(listWindow).OfType<Button>().Any(b => Equals(b.Content, "공지 전체 보기")), "The list has a footer link to all notices");
            fixture.InvalidNotifications = true; var count = fixture.NotificationLists;
            readAll.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitAsync(() => fixture.NotificationLists > count);
            check(list.Items.Count == 2 && Descendants(listWindow).OfType<TextBlock>().Any(t => t.IsVisible && t.Text.Contains("응답을 확인할 수 없습니다")),
                "A malformed notification response reports an error without clearing the prior list");
        }
        finally { closePost.Stop(); listWindow.Close(); }

        fixture.InvalidNotifications = false; fixture.FrameFixture = true;
        var inbox = new NotificationsDialog(new BoardClient(api), "");
        inbox.Show();
        try
        {
            var list = Descendants(inbox).OfType<ListBox>().Single();
            await WaitAsync(() => list.Items.Count == 4); inbox.UpdateLayout();
            Rect Bounds(FrameworkElement element) => new(element.TranslatePoint(new Point(), inbox), element.RenderSize);
            var rows = Descendants(list).OfType<NotificationView>().ToArray();
            var titles = list.Items.Cast<BoardAlert>().Select(n => n.Title).ToArray();
            check(inbox.RenderSize == new Size(400, 354) && rows.Length == 4 &&
                Bounds(rows[0]) == new Rect(1, 49, 398, 65) && Bounds(rows[3]) == new Rect(1, 244, 398, 65) &&
                titles[0] == "서버 점검 11.25 02:00~04:00" && titles[3] == "문의 답변 등록", "Board notices and answers are in timestamp order in four 65px Figma rows");
            check(Descendants(inbox).OfType<TextBlock>().Count(t => t.Text == "중요" && t.IsVisible) == 1 &&
                Descendants(inbox).OfType<TextBlock>().Any(t => t.Text == "2" && t.IsVisible), "Importance and unread count come from the feed");
            await WindowsSmoke.RenderAsync(inbox, Path.Combine(directory, "alert-list.png"));
            var openNotice = Descendants(rows[0]).OfType<Button>().Single();
            var detail = inbox.Dispatcher.InvokeAsync(async () =>
            {
                var dialog = inbox.OwnedWindows.OfType<BoardNoticeDialog>().Single();
                try
                {
                    var text = Descendants(dialog).OfType<TextBlock>().Select(t => t.Text).ToArray();
                    check(dialog.RenderSize == new Size(520, 238) && text.Contains("중요 공지") && text.Contains("2026-11-22") &&
                        text.Any(t => t.StartsWith("점검 동안 변환")), "Important notice shows the authorized detail title, date and body");
                    await WindowsSmoke.RenderAsync(dialog, Path.Combine(directory, "alert-important.png"));
                }
                finally { dialog.Close(); }
            }, DispatcherPriority.ApplicationIdle);
            openNotice.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await detail.Task.Unwrap();
            await WaitAsync(() => list.Items.OfType<BoardNoticeAlert>().Any(n => n.Read));
            check(list.Items.OfType<BoardNoticeAlert>().Single().Read, "Opening an announcement refreshes its server-confirmed read state");
            var overview = inbox.Dispatcher.InvokeAsync(async () =>
            {
                var boardWindow = inbox.OwnedWindows.OfType<BoardDialog>().Single();
                try
                {
                    await WaitAsync(() => Descendants(boardWindow).OfType<Button>().Any(b => b.IsVisible && Equals(b.Content, "공지") && System.Windows.Automation.AutomationProperties.GetItemStatus(b) == "선택됨"));
                    check(Descendants(boardWindow).OfType<Button>().Any(b => b.IsVisible && Equals(b.Content, "공지") && System.Windows.Automation.AutomationProperties.GetItemStatus(b) == "선택됨"),
                        "The footer opens the signed-in board on its notices tab");
                }
                finally { boardWindow.Close(); }
            }, DispatcherPriority.ApplicationIdle);
            Descendants(inbox).OfType<Button>().Single(b => Equals(b.Content, "공지 전체 보기")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await overview.Task.Unwrap();
        }
        finally { inbox.Close(); }
    }

    internal static async Task RunLiveAsync(string origin, string directory, Action<bool, string> check)
    {
        var service = new Uri(origin);
        if (!service.IsLoopback) throw new InvalidOperationException("공지 통합 검사는 로컬 시험 서버에서만 실행합니다.");
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var api = new SemojumApiClient(http, service);
        await api.LoginAsync(Environment.GetEnvironmentVariable("BOARD_NOTICE_QA_LOGIN")!, Environment.GetEnvironmentVariable("BOARD_NOTICE_QA_PASSWORD")!);
        var client = new BoardClient(api);
        var inbox = new NotificationsDialog(client, ""); inbox.Show();
        try
        {
            var list = Descendants(inbox).OfType<ListBox>().Single();
            const string title = "설치본 연동 시험 공지";
            await WaitAsync(() => list.Items.OfType<BoardNoticeAlert>().Any(n => n.Title == title));
            var notice = list.Items.OfType<BoardNoticeAlert>().Single(n => n.Title == title);
            check(notice.Important && !notice.Read, "The installed inbox renders the real account's unread important notice");
            var row = Descendants(inbox).OfType<Button>().Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == title);
            var child = inbox.Dispatcher.InvokeAsync(async () =>
            {
                await WaitAsync(() => inbox.OwnedWindows.OfType<BoardNoticeDialog>().Any(w => w.IsVisible));
                var dialog = inbox.OwnedWindows.OfType<BoardNoticeDialog>().Single(w => w.IsVisible);
                try
                {
                    check(Descendants(dialog).OfType<TextBlock>().Any(t => t.Text == "서버에서 가져온 공지 본문"), "Opening a notification retrieves the actual authorized detail");
                    await WindowsSmoke.RenderAsync(dialog, Path.Combine(directory, "important.png"));
                }
                finally { dialog.Close(); }
            }, DispatcherPriority.ApplicationIdle);
            row.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await child.Task.Unwrap();
            await WaitAsync(() => list.Items.OfType<BoardNoticeAlert>().Any(n => n.NoticeId == notice.NoticeId && n.Read));
            check(list.Items.OfType<BoardNoticeAlert>().Single(n => n.NoticeId == notice.NoticeId).Read, "The inbox reloads the confirmed read status from the database");
            Descendants(inbox).OfType<Button>().Single(b => Equals(b.Content, "모두 읽음")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitAsync(() => list.Items.OfType<BoardAlert>().All(n => n.Read));
            check((await client.NotificationsAsync()).UnreadCount == 0, "Mark all read persists for the authenticated account");
        }
        finally { inbox.Close(); }
    }

    private static async Task WaitAsync(Func<bool> ready)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!ready() && DateTime.UtcNow < deadline) await Task.Delay(20);
        await Task.Delay(50);
    }

    private static bool AnswerInquiryClose(Window inquiry, int choice, Action attempt)
    {
        var shown = false; var timedOut = false; var deadline = DateTime.UtcNow.AddSeconds(5); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        timer.Tick += (_, _) =>
        {
            if (DateTime.UtcNow >= deadline)
            {
                timedOut = true; timer.Stop();
                foreach (var child in Application.Current.Windows.Cast<Window>().Where(w => w.Owner == inquiry && w.IsVisible).ToArray()) child.Close();
                return;
            }
            var dialog = Application.Current.Windows.OfType<NoticeDialog>().FirstOrDefault(d => d.Owner == inquiry && d.IsVisible);
            if (dialog is null) return;
            shown = true; timer.Stop(); dialog.ActionButtons[choice].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        };
        timer.Start(); try { attempt(); } finally { timer.Stop(); }
        if (timedOut) throw new TimeoutException("Inquiry close confirmation did not finish within 5 seconds");
        return shown;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var item in Descendants(child)) yield return item; }
    }

    private sealed class Fixture : HttpMessageHandler
    {
        public int BoardLists { get; private set; }
        public int NotificationLists { get; private set; }
        public int PublicLists { get; private set; }
        public bool AuthenticatedNotifications { get; private set; }
        public bool PublicWasAnonymous { get; private set; }
        public int InquiryPosts { get; private set; }
        public bool InquiryWasAnonymous { get; private set; }
        public Dictionary<string, string>? Inquiry { get; private set; }
        public bool HoldNotifications { get; set; }
        public bool HoldPublic { get; set; }
        public bool DenyPublic { get; set; }
        public bool PopulatedPublic { get; set; }
        public bool Populated { get; set; }
        public bool DenyPost { get; set; }
        public bool DenyReadAll { get; set; }
        public bool InvalidNotifications { get; set; }
        public bool FrameFixture { get; set; }
        private bool openedFrameNotice;
        public bool MutationAuthorized { get; private set; }
        public int PostRequests { get; private set; }
        public int ReadAllRequests { get; private set; }
        private bool firstRead, secondRead;
        private readonly TaskCompletionSource<HttpResponseMessage> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<HttpResponseMessage> pendingPublic = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Release() => pending.TrySetResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{\"isSuccess\":true,\"result\":{\"unreadCount\":0,\"items\":[]}}", Encoding.UTF8, "application/json") });
        public void ReleasePublic() => pendingPublic.TrySetResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{\"isSuccess\":true,\"result\":[]}", Encoding.UTF8, "application/json") });
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath; string result;
            if (path.EndsWith("/login")) result = "{\"accessToken\":\"fixture\",\"refreshToken\":\"fixture\",\"role\":\"ROLE_USER\"}";
            else if (path == "/api/auth/social/providers") result = "[]";
            else if (path == "/api/board/posts") { BoardLists++; result = "{\"page\":1,\"totalCount\":0,\"notices\":[],\"items\":[]}"; }
            else if (path == "/api/board/notifications")
            {
                NotificationLists++; AuthenticatedNotifications = request.Headers.Authorization?.Scheme == "Bearer";
                if (request.Method == HttpMethod.Patch) throw new InvalidOperationException("Unexpected notification endpoint");
                if (HoldNotifications) return pending.Task.WaitAsync(cancellationToken);
                if (InvalidNotifications) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent("{\"isSuccess\":true,\"result\":{\"unreadCount\":0,\"items\":null}}", Encoding.UTF8, "application/json") });
                if (FrameFixture)
                {
                    var today = DateTime.Today;
                    var rows = new[] {
                        new BoardNotification(8, 80, "2026_수능특강_사회문화 변환 완료", 1, today.AddHours(8).AddMinutes(40).ToString("s"), false),
                        new BoardNotification(9, 81, "크레딧 잔여 안내", 1, new DateTime(today.Year - 1, 11, 20).ToString("s"), true),
                        new BoardNotification(10, 82, "문의 답변 등록", 1, new DateTime(today.Year - 1, 11, 18).ToString("s"), true)
                    };
                    var notices = new[] { new BoardNoticeAlert("fixture-notice", "서버 점검 11.25 02:00~04:00", today.AddHours(9).AddMinutes(12).ToString("s"), true, openedFrameNotice) };
                    result = JsonSerializer.Serialize(new { unreadCount = openedFrameNotice ? 1 : 2, items = rows, notices },
                        new JsonSerializerOptions(JsonSerializerDefaults.Web));
                }
                else result = !Populated ? "{\"unreadCount\":0,\"items\":[]}" : System.Text.Json.JsonSerializer.Serialize(new
                {
                    unreadCount = (firstRead ? 0 : 1) + (secondRead ? 0 : 1), items = new[]
                    {
                        new { notificationId = 1, postId = 77, title = "첫 문의", answerCount = 1, lastAnsweredAt = "2026-10-29", read = firstRead },
                        new { notificationId = 2, postId = 78, title = "둘째 문의", answerCount = 1, lastAnsweredAt = "2026-10-29", read = secondRead }
                    }
                });
            }
            else if (path == "/api/board/notices/fixture-notice")
            {
                openedFrameNotice = true;
                result = JsonSerializer.Serialize(new { title = "11월 25일 02:00~04:00 서버 점검", createdAt = "2026-11-22", important = true,
                    body = "점검 동안 변환 · 서버 저장 · 게시판을 쓸 수 없습니다. 열어 둔 문서는 이 컴퓨터에 저장해 두십시오." });
            }
            else if (path == "/api/board/posts/77")
            {
                PostRequests++;
                if (DenyPost) return Task.FromResult(Error());
                firstRead = true;
                result = "{\"postId\":77,\"title\":\"첫 문의\",\"content\":\"본문\",\"author\":\"fixture\",\"mine\":true,\"secret\":true,\"hidden\":false,\"createdAt\":\"2026-10-29\",\"updatedAt\":\"2026-10-29\",\"viewCount\":1,\"attachments\":[],\"answers\":[]}";
            }
            else if (path == "/api/board/notifications/read-all")
            {
                if (request.Method != HttpMethod.Patch) throw new InvalidOperationException("Read-all must use PATCH");
                MutationAuthorized = request.Headers.Authorization?.Scheme == "Bearer";
                ReadAllRequests++;
                if (DenyReadAll) return Task.FromResult(Error());
                firstRead = secondRead = true; result = "null";
            }
            else if (path == "/api/public/notices")
            {
                PublicLists++; PublicWasAnonymous = request.Headers.Authorization is null;
                if (HoldPublic) return pendingPublic.Task.WaitAsync(cancellationToken);
                if (DenyPublic) return Task.FromResult(Error());
                result = PopulatedPublic ? "[{\"title\":\"점검 공지\",\"body\":\"안내 본문\",\"createdAt\":\"2026-10-24T15:00:00Z\"},{\"title\":\"두 번째 공지\",\"body\":\"자세한 본문\",\"createdAt\":\"2026-10-01T09:00:00Z\"},{\"title\":\"날짜 없는 공지\",\"body\":\"본문\"}]" : "[]";
            }
            else if (path == "/api/public/inquiries")
            { InquiryPosts++; InquiryWasAnonymous = request.Headers.Authorization is null; return ReceiveInquiryAsync(request, cancellationToken); }
            else throw new InvalidOperationException("Unexpected fixture path " + path);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{\"isSuccess\":true,\"result\":" + result + "}", Encoding.UTF8, "application/json") });
        }
        private async Task<HttpResponseMessage> ReceiveInquiryAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Inquiry = await WindowsPublicInquiryFieldsChecks.FormAsync(request.Content!, cancellationToken);
            return Error();
        }
        private static HttpResponseMessage Error() => new(HttpStatusCode.Forbidden)
        { Content = new StringContent("{\"isSuccess\":false,\"code\":\"COMMON4003\",\"message\":\"접근할 수 없습니다\"}", Encoding.UTF8, "application/json") };
    }
}
