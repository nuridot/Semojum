using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Semojum.Core.Integration;
using Semojum.Desktop.Dialogs;

namespace Semojum.Desktop.Services;

internal static class WindowsAuthenticationFormChecks
{
    internal static async Task RunAsync(Action<bool, string> check, string directory)
    {
        var fake = new EmailServer();
        using var http = new HttpClient(fake) { BaseAddress = new Uri("https://example.invalid") };
        var api = new SemojumApiClient(http);
        var owner = new MainWindow(smoke: true, testWithoutAccount: true, startEmpty: true);
        owner.Show();
        var login = new AccountDialog(api, new WindowsLoginDesignChecks.MemoryStore(), null);
        var session = login.ShowEmbeddedAsync(owner);
        var captureDir = Environment.GetEnvironmentVariable("AUTH_VISUAL_DIR") ?? directory;
        Directory.CreateDirectory(captureDir);

        async Task Layout()
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            owner.UpdateLayout();
        }
        T WithId<T>(string id) where T : FrameworkElement => Children(owner).OfType<T>().Single(e => AutomationProperties.GetAutomationId(e) == id);
        T Named<T>(FrameworkElement root, string name) where T : FrameworkElement => Children(root).OfType<T>().Single(e => AutomationProperties.GetName(e) == name);
        Button ButtonIn(FrameworkElement root, string label) => Children(root).OfType<Button>().Single(b => Equals(b.Content, label));
        static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        async Task Navigate(string name)
        {
            var source = WithId<Button>("LoginNavigation"); Click(source); await Layout();
            var command = source.ContextMenu.Items.OfType<MenuItem>().Single(m => Equals(m.Header, name));
            check(source.ContextMenu.IsOpen && command.IsVisible && command.IsEnabled, "Account menu offers " + name);
            source.ContextMenu.IsOpen = false; command.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); await Layout();
        }
        Rect Bounds(FrameworkElement e) => e.TransformToAncestor(owner).TransformBounds(new Rect(0,0,e.ActualWidth,e.ActualHeight));
        void At(FrameworkElement e, double x, double y, double width, double height, string label)
        {
            var r = Bounds(e);
            check(Math.Abs(r.X-x) <= 2 && Math.Abs(r.Y-y) <= 2 && Math.Abs(r.Width-width) <= 2 && Math.Abs(r.Height-height) <= 2,
                $"{label} matches Figma ({x},{y} {width}x{height}), rendered {r.X:F1},{r.Y:F1} {r.Width:F1}x{r.Height:F1}");
        }
        async Task Capture(string name) => await WindowsSmoke.RenderElementAsync(owner, Path.Combine(captureDir, name + ".png"));
        static async Task Until(Func<bool> predicate)
        {
            var deadline = DateTime.UtcNow.AddSeconds(8);
            while (!predicate() && DateTime.UtcNow < deadline) await Task.Delay(15);
            if (!predicate()) throw new TimeoutException("Email form did not finish its local request.");
        }
        try
        {
            await Layout();
            owner.WindowState = WindowState.Normal;
            foreach (var (width,height) in new[] { (1440,1000), (1280,800) })
            {
                owner.Width = width; owner.Height = height; await Layout();
                var dx = (width-1440)/2; var dy = (height-1000)/2;
                var card = WithId<Border>("LoginCard");
                At(card,510+dx,195+dy,420,594,"A1 login card");
                At(Named<Button>(card,"Google로 계속하기"),571+dx,300+dy,298,40,"A1 Google");
                At(Named<Button>(card,"카카오 로그인"),571+dx,348+dy,300,41,"A1 Kakao");
                At(Named<Button>(card,"네이버 로그인"),571+dx,397+dy,300,39,"A1 Naver");
                At(Named<TextBox>(card,"아이디").Parent as Border ?? throw new InvalidOperationException(),571+dx,504+dy,298,40,"A1 login ID");
                At(ButtonIn(card,"로그인"),571+dx,636+dy,298,40,"A1 primary action");
                At(Named<Button>(owner,"문의하기"),670+dx,805+dy,100,32,"A1 public inquiry");
                await Capture($"auth-login-{width}x{height}");
                await Navigate("회원가입");
                var signup = WithId<Border>("SignupCard");
                At(signup,510+dx,237+dy,420,558,"A2 signup card");
                At(Named<TextBox>(signup,"이메일").Parent as Border ?? throw new InvalidOperationException(),571+dx,362+dy,298,40,"A2 email");
                At(ButtonIn(signup,"회원가입"),571+dx,714+dy,298,40,"A2 primary action");
                var consents = Children(signup).OfType<CheckBox>().ToArray();
                check(consents.Length == 4 && consents.All(c => c.IsChecked != true), "A2 opens with explicit consent for each new signup");
                At(consents[0],583+dx,582+dy,274,20,"A2 agreement group");
                await Capture($"auth-signup-{width}x{height}");
                if (width == 1440)
                {
                    var view = Named<Button>(signup, "[선택] 광고성 정보 수신 동의 보기");
                    async Task Choose(string? label, bool expected, bool capture = false)
                    {
                        var inspect = owner.Dispatcher.InvokeAsync(async () =>
                        {
                            var dialog = owner.OwnedWindows.OfType<LegalDocumentDialog>().Single();
                            try
                            {
                                check(dialog.Kind == LegalDocumentKind.Marketing && dialog.RenderSize == new Size(520, 418) &&
                                    dialog.Reader.Document.Blocks.OfType<System.Windows.Documents.Section>().Count() == 3,
                                    "Signup opens the three marketing consent sections in the 520×418 Figma frame");
                                if (capture) await WindowsSmoke.RenderAsync(dialog, Path.Combine(directory, "alert-marketing.png"));
                                if (label is not null) Click(ButtonIn(dialog, label));
                            }
                            finally { if (dialog.IsVisible) dialog.Close(); }
                        }, DispatcherPriority.ApplicationIdle);
                        Click(view); await inspect.Task.Unwrap();
                        check(consents[3].IsChecked == expected && consents[1].IsChecked != true && consents[2].IsChecked != true && consents[0].IsChecked != true,
                            "The requested marketing choice is reflected on signup, independently of required consent");
                    }
                    await Choose("동의", true, true);
                    await Choose(null, true);
                    await Choose("동의 안 함", false);
                }
                await Navigate("로그인으로 돌아가기");
            }
            var loginCard = WithId<Border>("LoginCard");
            Click(ButtonIn(loginCard,"로그인")); await Layout();
            check(fake.Paths.All(path => path == "/api/auth/social/providers") && Children(loginCard).OfType<TextBlock>().Any(t => t.IsVisible && t.Text.Contains("아이디와 비밀번호를 입력")),
                "A1 required-field feedback appears inside the card without sending a login attempt");
            await Capture("auth-login-validation");

            await Navigate("비밀번호 찾기");
            var reset = WithId<Border>("ResetCard");
            Named<TextBox>(reset,"이메일").Text = "qa@example.test";
            Click(ButtonIn(reset,"인증 번호 받기")); await Until(() => Children(reset).OfType<TextBox>().Any(c => AutomationProperties.GetName(c) == "이메일 인증 번호 6자리"));
            Click(ButtonIn(reset,"인증 번호 재발송")); await Until(() => fake.Paths.Contains("/api/auth/password/reset/resend"));
            Named<PasswordBox>(reset,"새 비밀번호").Password = Named<PasswordBox>(reset,"비밀번호 확인").Password = "test-password";
            Named<TextBox>(reset,"이메일 인증 번호 6자리").Text = "123456";
            Click(ButtonIn(reset,"비밀번호 재설정")); await Until(() => Children(owner).OfType<Border>().Any(b => AutomationProperties.GetAutomationId(b) == "LoginCard"));
            check(api.Account is null && fake.Paths.Contains("/api/auth/password/reset/confirm") && Named<TextBox>(WithId<Border>("LoginCard"),"아이디").Text == "qa@example.test",
                "Completing a password reset returns to login with the verified address and without signing in");

            await Navigate("회원가입");
            var form = WithId<Border>("SignupCard"); var identity = Named<TextBox>(form,"이메일");
            identity.Text = "qa@example.test"; Named<PasswordBox>(form,"비밀번호").Password = Named<PasswordBox>(form,"비밀번호 확인").Password = "test-password";
            Click(ButtonIn(form,"회원가입")); await Layout();
            check(!fake.Paths.Contains("/api/auth/email/signup") && Children(form).OfType<TextBlock>().Any(t => t.IsVisible && t.Text.Contains("필수 약관")), "Signup validates required consent before sending the challenge");
            foreach (var option in Children(form).OfType<CheckBox>().Where(c => c.Content is string text && text.StartsWith("[필수]"))) { option.IsChecked = true; option.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent)); }
            Click(ButtonIn(form,"회원가입"));
            await Until(() => Children(form).OfType<TextBox>().Any(c => AutomationProperties.GetName(c) == "이메일 인증 번호 6자리"));
            check(identity.IsReadOnly && fake.ConsentSent && !Children(form).OfType<PasswordBox>().Any(), "A2 starts an email challenge using the selected agreements, with optional marketing consent omitted");
            check(Children(form).OfType<TextBlock>().Any(t => t.IsVisible && t.Text == "이메일을 확인해 주세요." && t.Foreground is SolidColorBrush { Color: var color } && color == Color.FromRgb(95,95,95)),
                "An email challenge displays its next step in the secondary text color");
            await Capture("auth-signup-verification");
            var pin = Named<TextBox>(form,"이메일 인증 번호 6자리"); pin.Text = "123";
            Click(ButtonIn(form,"인증하고 가입")); await Layout();
            check(!fake.Paths.Contains("/api/auth/email/signup/verify") && Children(form).OfType<TextBlock>().Any(t => t.IsVisible && t.Text == "인증 번호 6자리를 입력해 주세요." && t.Foreground is SolidColorBrush { Color: var color } && color == Color.FromRgb(196,43,28)), "A short verification code stays in the form with the error text color");
            pin.Text = "123456"; Click(ButtonIn(form,"인증하고 가입"));
            check(await session.WaitAsync(TimeSpan.FromSeconds(8)) && api.Account?.LoginId == "qa@example.test" && !Children(owner).OfType<Button>().Any(b => AutomationProperties.GetAutomationId(b) == "LoginNavigation"),
                "A2 verifies the new account and restores the editor caption through the production embedded host");
        }
        finally { owner.PrepareSmokeShutdown(); owner.Close(); }
    }

    private static IEnumerable<DependencyObject> Children(DependencyObject root)
    {
        yield return root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Children(VisualTreeHelper.GetChild(root,i))) yield return child;
    }

    private sealed class EmailServer : HttpMessageHandler
    {
        internal readonly List<string> Paths = [];
        internal bool ConsentSent;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var path = request.RequestUri!.AbsolutePath; Paths.Add(path);
            if (path == "/api/auth/email/signup") {
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
                var agreements = body.RootElement.GetProperty("agreements");
                ConsentSent = agreements.GetProperty("termsVersion").GetString() is not null && agreements.GetProperty("privacyVersion").GetString() is not null
                    && (!agreements.TryGetProperty("marketingVersion", out var marketing) || marketing.ValueKind == JsonValueKind.Null);
            }
            var result = path switch {
                "/api/auth/social/providers" => """[{"provider":"NAVER","clientId":"n"},{"provider":"KAKAO","clientId":"k"},{"provider":"GOOGLE","clientId":"g"}]""",
                "/api/auth/email/signup/verify" => """{"accessToken":"test","refreshToken":"test","role":"ROLE_USER"}""",
                "/api/auth/password/reset/confirm" => "null",
                "/api/auth/email/signup" or "/api/auth/password/reset" or "/api/auth/password/reset/resend" => """{"verificationToken":"test","expiresInSeconds":300,"resendAfterSeconds":0,"maxAttempts":5}""",
                _ => throw new InvalidOperationException("Unexpected test request " + path)
            };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"isSuccess\":true,\"result\":" + result + "}", Encoding.UTF8, "application/json") };
        }
    }
}
