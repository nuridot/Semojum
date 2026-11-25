using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Semojum.Core.Integration;
using Semojum.Desktop.Dialogs;
using Semojum.Feature.Account.Wpf;

namespace Semojum.Desktop.Services;

internal static class WindowsSocialLoginChecks
{
    internal static async Task RunAsync(Action<bool, string> check)
    {
        using var browser = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(5) };
        foreach (var provider in Enum.GetValues<SocialProvider>())
        {
            var server = new Server(provider);
            using var http = new HttpClient(server);
            var api = new SemojumApiClient(http);
            var store = new WindowsLoginDesignChecks.MemoryStore();
            var host = new BrowserHost();
            var owner = new MainWindow(smoke: true, testWithoutAccount: true, startEmpty: true);
            owner.Show();
            try
            {
                var login = new AccountDialog(api, store, null, host: host);
                var session = login.ShowEmbeddedAsync(owner, allowCancel: true);
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                var button = Children<Button>(owner).Single(b => AutomationProperties.GetAutomationId(b) == "LoginSocial" + provider);
                check(Children<Button>(owner).Count(b => AutomationProperties.GetAutomationId(b).StartsWith("LoginSocial")) == 1,
                    provider + " is the only enabled and supported provider");
                var remember = provider != SocialProvider.Google;
                Children<CheckBox>(owner).Single(c => Equals(c.Content, "로그인 상태 유지")).IsChecked = remember;
                Click(button);
                var address = host.Address ?? throw new InvalidOperationException("Browser did not open.");
                var query = HttpUtility.ParseQueryString(address.Query);
                var redirect = query["redirect_uri"]!;
                var expected = provider switch { SocialProvider.Google => "accounts.google.com/o/oauth2/v2/auth",
                    SocialProvider.Kakao => "kauth.kakao.com/oauth/authorize", _ => "nid.naver.com/oauth2/authorize" };
                check(address.Scheme == "https" && address.Host + address.AbsolutePath == expected && query["response_type"] == "code" &&
                    query["client_id"] == "fixture" && query["code_challenge_method"] == "S256" && query["state"]?.Length >= 43 &&
                    query["scope"] == (provider == SocialProvider.Google ? "openid email profile" : provider == SocialProvider.Naver ? "openid" : null) &&
                    (provider == SocialProvider.Google ? new Uri(redirect).Host == "127.0.0.1" && new Uri(redirect).Port > 0 : redirect == "http://localhost:53682/callback"),
                    provider + " uses its desktop authorization endpoint, scope and registered redirect");
                using var stray = await browser.GetAsync(redirect + "?state=wrong&code=stray");
                check(stray.StatusCode == HttpStatusCode.BadRequest && server.Exchanges == 0, provider + " ignores an unrelated callback");
                if (provider == SocialProvider.Google)
                {
                    var cancel = Children<Button>(owner).Single(b => Equals(b.Content, "브라우저 로그인 취소"));
                    Click(cancel); await Until(() => button.IsEnabled);
                    check(server.Exchanges == 0, "Cancelling in the app releases the pending callback");
                    Click(button);
                    query = HttpUtility.ParseQueryString(host.Address!.Query); redirect = query["redirect_uri"]!;
                    var originalState = query["state"];
                    using var denied = await browser.GetAsync(redirect + "?error=access_denied&state=" + originalState);
                    await Until(() => button.IsEnabled);
                    check(denied.IsSuccessStatusCode && server.Exchanges == 0 && api.Account is null, "Browser cancellation leaves the login form available");
                    Click(button);
                    query = HttpUtility.ParseQueryString(host.Address!.Query); redirect = query["redirect_uri"]!;
                    check(originalState != query["state"], "Retry uses a new state");
                    using var conflict = await browser.GetAsync(redirect + "?code=conflict&state=" + query["state"]);
                    await Until(() => button.IsEnabled);
                    check(api.Account is null && Children<TextBlock>(owner).Any(t => t.IsVisible && t.Text.Contains("아이디와 비밀번호") && t.Text.Contains("네이버")),
                        "An email conflict displays the login methods returned by the server");
                    Click(button);
                    query = HttpUtility.ParseQueryString(host.Address!.Query); redirect = query["redirect_uri"]!;
                }
                using var accepted = await browser.GetAsync(redirect + "?code=sample&state=" + query["state"]);
                if (provider != SocialProvider.Google)
                {
                    await Until(() => owner.OwnedWindows.OfType<Window>().Any(w => w.Title == "소셜 회원가입"));
                    var consent = owner.OwnedWindows.OfType<Window>().Single(w => w.Title == "소셜 회원가입");
                    var submit = Children<Button>(consent).Single(b => Equals(b.Content, "가입하기"));
                    Click(submit);
                    check(server.Signups == 0 && api.Account is null && Children<TextBlock>(consent).Any(t => t.Text.Contains("first@example.test")),
                        "First login shows the provider profile and waits for explicit consent");
                    foreach (var box in Children<CheckBox>(consent).Where(c => c.Content is string s && s.StartsWith("[필수]"))) box.IsChecked = true;
                    if (provider == SocialProvider.Naver)
                        Children<CheckBox>(consent).Single(c => c.Content is string s && s.StartsWith("[선택]")).IsChecked = true;
                    Click(submit);
                }
                check(await session.WaitAsync(TimeSpan.FromSeconds(8)) && accepted.IsSuccessStatusCode &&
                    api.Account is { LoginId: "canonical", MemberType: "PERSONAL" } && (store.Value?.LoginId == "canonical") == remember,
                    provider + " returns to the editor with the server identity and honors the remember choice");
                var challenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(server.Verifier!))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                check(server.Exchanges == 1 && server.Redirect == redirect && query["code_challenge"] == challenge &&
                    server.Keep == remember && (provider == SocialProvider.Google ? server.Signups == 0 : server.Signups == 1 && server.ValidSignup),
                    provider + " sends the matching verifier, callback and consent payload");
            }
            finally { owner.PrepareSmokeShutdown(); owner.Close(); }
        }
    }

    private sealed class BrowserHost : IAccountScreenHost
    {
        public Uri? Address;
        public Version Version => MainWindow.CurrentVersion;
        public void OpenSocialBrowser(Uri address) => Address = address;
        public void ShowNotices(Window owner, SemojumApiClient api) => throw new NotSupportedException();
        public void ShowInquiry(Window owner, SemojumApiClient api) => throw new NotSupportedException();
        public NoticeDialog LoginRejected(SemojumApiException error) => throw new NotSupportedException();
    }

    private sealed class Server(SocialProvider provider) : HttpMessageHandler
    {
        internal int Exchanges, Signups;
        internal bool Keep, ValidSignup;
        internal string? Verifier, Redirect;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            var path = req.RequestUri!.AbsolutePath;
            var body = req.Content is not null ? await req.Content.ReadFromJsonAsync<JsonElement>(ct) : default;
            var loggedIn = new { status = "LOGGED_IN", accessToken = "fixture-a", refreshToken = "fixture-r", role = "ROLE_USER", memberType = "PERSONAL" };
            object result;
            if (path == "/api/auth/social/providers")
                result = new[] { new { provider = "OTHER", clientId = "x" }, new { provider = provider.ToString().ToUpperInvariant(), clientId = "fixture" } };
            else if (path == "/api/auth/social/login/" + provider.ToString().ToLowerInvariant())
            {
                if (body.GetProperty("code").GetString() == "conflict")
                    return new(HttpStatusCode.Conflict) { Content = JsonContent.Create(new { isSuccess = false, code = "SOCIAL4091",
                        message = "이미 가입했습니다.", result = new { existingLoginMethods = new[] { "PASSWORD", "NAVER" } } }) };
                Exchanges++;
                if (body.GetProperty("code").GetString() != "sample") throw new InvalidOperationException("Wrong authorization code.");
                Keep = body.GetProperty("keepLoggedIn").GetBoolean(); Verifier = body.GetProperty("codeVerifier").GetString();
                Redirect = body.GetProperty("redirectUri").GetString();
                result = provider == SocialProvider.Google ? loggedIn : new { status = "SIGNUP_REQUIRED", provider = provider.ToString().ToUpperInvariant(), signupToken = "ticket", email = "first@example.test", nickname = "점역사" };
            }
            else if (path == "/api/auth/social/signup")
            {
                Signups++; var agreements = body.GetProperty("agreements");
                ValidSignup = body.GetProperty("signupToken").GetString() == "ticket" && body.GetProperty("keepLoggedIn").GetBoolean() == Keep &&
                    agreements.GetProperty("termsVersion").GetString() == "2026-11-09" && agreements.GetProperty("privacyVersion").GetString() == "2026-11-09" &&
                    (provider == SocialProvider.Naver ? agreements.GetProperty("marketingVersion").GetString() == "2026-11-25" : !agreements.TryGetProperty("marketingVersion", out _));
                result = loggedIn;
            }
            else if (path == "/api/auth/refresh" && body.GetProperty("refreshToken").GetString() == "fixture-r")
                result = new { loginId = "canonical", role = "ROLE_USER", accessToken = "new-access", memberType = "PERSONAL" };
            else throw new InvalidOperationException("Unexpected request " + path);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { isSuccess = true, result }) };
        }
    }

    private static async Task Until(Func<bool> ready)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (!ready()) await Task.Delay(15, timeout.Token);
    }
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static IEnumerable<T> Children<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T item) yield return item;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Children<T>(VisualTreeHelper.GetChild(root,i))) yield return child;
    }
}
