using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Shell;
using Semojum.Core.Integration;
using Semojum.Feature.Account;
using Semojum.Feature.Account.Wpf;
using Semojum.Desktop.Services;
using SharpVectors.Converters;

namespace Semojum.Desktop.Dialogs;

public sealed class AccountDialog : Window
{
    private Grid? embeddedScreen;
    private readonly DockPanel authCaption;
    private PasswordBox? embeddedPassword;
    private CancellationTokenSource? embeddedLifetime;
    private TaskCompletionSource<bool>? embeddedResult;
    public AccountDialog(SemojumApiClient api) : this(api, null, null) { }
    public AccountDialog(SemojumApiClient api, IAccountCredentialStore? credentials, string? initialMessage, bool allowInstitution = false, IAccountScreenHost? host = null)
    {
        host ??= AccountScreenServices.Host;
        Title = "누리닷 로그인"; Width = 1440; Height = 1000; MinWidth = 420; MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.CanResize; WindowStyle = WindowStyle.None;
        WindowBounds.UseMonitorWorkArea(this);
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 32, GlassFrameThickness = new(0), CornerRadius = new(0),
            ResizeBorderThickness = new(4), UseAeroCaptionButtons = false });
        Background = Brush("#F7F9FC");
        var root = new Grid(); root.RowDefinitions.Add(new() { Height = new(32) }); root.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        Content = root;
        var heading = authCaption = new DockPanel { Background = Brush("#F3F3F3") };
        var captionButtons = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(captionButtons, Dock.Right); heading.Children.Add(captionButtons);
        void Caption(string asset, string label, Action action)
        {
            var button = new Button { Content = DesignIcon("login-caption-" + asset + ".svg", 16), Width = 46, Height = 32, MinWidth = 0, MinHeight = 0,
                Padding = new(0), Margin = new(0), Background = Brushes.Transparent, BorderBrush = Brushes.Transparent,
                Style = (Style)FindResource("ToolButton"), ToolTip = label };
            AutomationProperties.SetName(button, label); AutomationProperties.SetAutomationId(button, "LoginCaption" + asset);
            WindowChrome.SetIsHitTestVisibleInChrome(button, true); button.Click += (_, _) => action(); captionButtons.Children.Add(button);
        }
        Window CaptionOwner() => Window.GetWindow(heading) ?? this;
        Caption("min", "최소화", () => SystemCommands.MinimizeWindow(CaptionOwner()));
        Caption("max", "최대화 / 복원", () => { var owner = CaptionOwner(); if (owner.WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(owner); else SystemCommands.MaximizeWindow(owner); });
        Caption("close", "닫기", () => SystemCommands.CloseWindow(CaptionOwner()));
        var brand = new StackPanel { Orientation = Orientation.Horizontal };
        brand.Children.Add(WindowFrame.CreateBrandSymbol());
        brand.Children.Add(new TextBlock { Text = "누리닷", FontSize = 12, Foreground = Brush("#5F5F5F"), Margin = new(8,0,0,0), VerticalAlignment = VerticalAlignment.Center });
        var navigation = new Button { Content = brand, Style = (Style)FindResource("ToolButton"), MinWidth = 0, MinHeight = 0, Height = 32,
            HorizontalAlignment = HorizontalAlignment.Left, Padding = new(0), Margin = new(12,0,0,0), Background = Brushes.Transparent, BorderBrush = Brushes.Transparent,
            ToolTip = "회원가입 · 비밀번호 찾기 · 공지사항 (F10)" };
        AutomationProperties.SetName(navigation, "누리닷 메뉴"); AutomationProperties.SetAutomationId(navigation, "LoginNavigation");
        WindowChrome.SetIsHitTestVisibleInChrome(navigation, true); heading.Children.Add(navigation);
        var menu = navigation.ContextMenu = new ContextMenu { PlacementTarget = navigation, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        navigation.Click += (_, _) => menu.IsOpen = true;
        root.Children.Add(new Border { Background = Brush("#F3F3F3"), BorderBrush = Brush("#EAEAEA"), BorderThickness = new(0, 0, 0, 1), Child = heading });
        var screen = new Grid { Background = Brush("#F7F9FC") }; embeddedScreen = screen; Grid.SetRow(screen, 1); root.Children.Add(screen);
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center };
        screen.Children.Add(scroll);
        var group = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new(12,16,12,16) };
        scroll.Content = group;
        var fields = new StackPanel(); var card = new Border { Child = fields }; AccountFormElements.StyleCard(card); group.Children.Add(card);
        AutomationProperties.SetAutomationId(card, "LoginCard"); fields.Children.Add(AccountFormElements.Logo());
        var social = new SocialLoginButtons(); fields.Children.Add(social);
        var login = new TextBox(); fields.Children.Add(AccountFormElements.Field("아이디", login));
        var password = embeddedPassword = new PasswordBox(); fields.Children.Add(AccountFormElements.Field("비밀번호", password));
        var error = new TextBlock { Text = initialMessage ?? "" }; AccountFormElements.StyleFeedback(error); fields.Children.Add(error);
        var submit = AccountFormElements.ActionButton("로그인", true); submit.IsDefault = true; submit.Margin = new(0,16,0,0); fields.Children.Add(submit);
        var remember = new CheckBox { Content = "로그인 상태 유지", IsChecked = credentials is not null, IsEnabled = credentials is not null,
            Margin = new(0,16,0,0), Style = AccountFormElements.CheckStyle() };
        AutomationProperties.SetName(remember, "로그인 상태 유지"); fields.Children.Add(remember);
        if (credentials is null) remember.ToolTip = "이 창에서는 로그인 정보를 저장하지 않습니다.";
        var cancelBrowser = AccountFormElements.ActionButton("브라우저 로그인 취소", false);
        cancelBrowser.Visibility = Visibility.Collapsed; fields.Children.Insert(fields.Children.IndexOf(submit), cancelBrowser);
        CancellationTokenSource? browserAttempt = null;
        cancelBrowser.Click += (_, _) => browserAttempt?.Cancel();
        var legalLinks = new Grid { Height = 20, Margin = new(0,16,0,0) };
        void LegalLink(string title, LegalDocumentKind kind, bool right)
        {
            var button = new Button { Content = title, FontSize = right ? 14 : 12, FontWeight = right ? FontWeights.SemiBold : FontWeights.Normal,
                Height = right ? 20 : 16, MinHeight = 0, MinWidth = 0, Padding = new(0), Margin = new(0),
                HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                Background = Brushes.Transparent, BorderBrush = Brushes.Transparent, Template = FigmaDialogElements.SurfaceTemplate(0) };
            AutomationProperties.SetName(button, title); AutomationProperties.SetAutomationId(button, "LoginLegal" + kind);
            button.Click += (_, _) =>
            {
                try { new LegalDocumentDialog(kind) { Owner = Window.GetWindow(screen) ?? this }.ShowDialog(); }
                finally { button.Focus(); }
            };
            legalLinks.Children.Add(button);
        }
        LegalLink("이용약관", LegalDocumentKind.Terms, false); LegalLink("개인정보 처리방침", LegalDocumentKind.Privacy, true);
        fields.Children.Add(legalLinks);
        Window ActiveOwner() => Window.GetWindow(screen) ?? this;
        void OpenEmailForm(bool signup)
        {
            password.Clear();
            scroll.ScrollToTop();
            scroll.Content = new EmailAccountForm(api, signup, login.Text, (signedIn, address, message) =>
            {
                if (signedIn)
                {
                    if (embeddedResult is { } result) result.TrySetResult(true); else DialogResult = true;
                }
                else
                {
                    scroll.Content = group; scroll.ScrollToTop(); login.Text = address; AccountFormElements.SetFeedback(error, message ?? "", information: message is not null);
                    var next = address.Length == 0 ? (Control)login : password;
                    Dispatcher.InvokeAsync(() => { if (next.IsVisible) next.Focus(); }, System.Windows.Threading.DispatcherPriority.Input);
                }
            });
        }
        MenuItem Command(string title, Action action)
        {
            var command = new MenuItem { Header = title }; command.Click += (_, _) => action(); menu.Items.Add(command); return command;
        }
        var backToLogin = Command("로그인으로 돌아가기", () => (scroll.Content as EmailAccountForm)?.GoBack());
        var signUp = Command("회원가입", () => OpenEmailForm(true));
        var reset = Command("비밀번호 찾기", () => OpenEmailForm(false));
        var announcements = Command("공지사항", () => host.ShowNotices(ActiveOwner(), api)); announcements.Icon = DesignIcon("login-bell.svg", 16);

        menu.Opened += (_, _) =>
        {
            var onLogin = ReferenceEquals(scroll.Content, group);
            signUp.Visibility = reset.Visibility = onLogin ? Visibility.Visible : Visibility.Collapsed;
            backToLogin.Visibility = onLogin ? Visibility.Collapsed : Visibility.Visible;
            signUp.IsEnabled = reset.IsEnabled = submit.IsEnabled;
        };
        void NavigationKeys(object sender, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.F10 && Keyboard.Modifiers == ModifierKeys.None) { e.Handled = true; navigation.Focus(); menu.IsOpen = true; }
            else if (scroll.Content is EmailAccountForm form && (key == Key.Escape || key == Key.Left && Keyboard.Modifiers == ModifierKeys.Alt))
            { e.Handled = true; form.GoBack(); }
        }
        screen.PreviewKeyDown += NavigationKeys; heading.PreviewKeyDown += NavigationKeys;
        var inquiryLabel = new StackPanel { Orientation = Orientation.Horizontal };
        inquiryLabel.Children.Add(DesignIcon("login-mail.svg", 16)); inquiryLabel.Children.Add(new TextBlock { Text = "문의하기", Margin = new(8,0,0,0), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        var inquiry = AccountFormElements.ActionButton(inquiryLabel, false); inquiry.Width = 100; inquiry.Margin = new(0,16,0,0);
        AutomationProperties.SetName(inquiry, "문의하기"); inquiry.Click += (_, _) => host.ShowInquiry(ActiveOwner(), api);
        group.Children.Add(inquiry);
        var version = new TextBlock { Text = "V" + host.Version.ToString(3), FontSize = 12, Foreground = Brush("#8A8A8A"),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new(0, 0, 40, 16), IsHitTestVisible = false };
        screen.Children.Add(version);
        var lifetime = new CancellationTokenSource(); embeddedLifetime = lifetime;
        Closed += (_, _) => { lifetime.Cancel(); password.Clear(); };
        bool loadingProviders = false;

        async Task LoadProvidersAsync()
        {
            if (loadingProviders || lifetime.IsCancellationRequested) return;
            loadingProviders = true;
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(8));
                var clients = await api.GetSocialProvidersAsync(timeout.Token);
                if (lifetime.IsCancellationRequested) return;
                social.ShowProviders(clients, client => SignInAsync(client));
            }
            catch (Exception ex) when (ex is HttpRequestException or SemojumApiException or OperationCanceledException or System.Text.Json.JsonException or ObjectDisposedException)
            {
                if (!lifetime.IsCancellationRequested) social.ShowRetry(LoadProvidersAsync);
            }
            finally { loadingProviders = false; }
        }
        async Task<AccountIdentity?> SignInSocialAsync(SocialClient client, bool keep)
        {
            password.Clear();
            SocialAuthorization authorization;
            using (var attempt = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
            {
                browserAttempt = attempt; cancelBrowser.Visibility = Visibility.Visible;
                AccountFormElements.SetFeedback(error, "기본 브라우저에서 로그인을 완료해 주세요.", information: true);
                cancelBrowser.Focus();
                try { authorization = await SocialBrowserLogin.AuthorizeAsync(client, host.OpenSocialBrowser, attempt.Token); }
                catch (OperationCanceledException) when (!lifetime.IsCancellationRequested)
                {
                    AccountFormElements.SetFeedback(error, "소셜 로그인을 취소했습니다.", information: true);
                    return null;
                }
                finally { browserAttempt = null; cancelBrowser.Visibility = Visibility.Collapsed; }
            }
            lifetime.Token.ThrowIfCancellationRequested();
            ActiveOwner().Activate();
            AccountFormElements.SetFeedback(error, "로그인 정보를 확인하고 있습니다.", information: true);
            var result = await api.LoginSocialAsync(client.Provider, authorization.Code, authorization.Verifier, authorization.RedirectUri, keep, lifetime.Token);
            if (result.Account is { } account) return account;
            lifetime.Token.ThrowIfCancellationRequested();
            var signup = new SocialSignupDialog(result.Signup!) { Owner = ActiveOwner() };
            if (signup.ShowDialog() != true)
            {
                AccountFormElements.SetFeedback(error, "회원가입을 취소했습니다.", information: true);
                return null;
            }
            return await api.SignupSocialAsync(result.Signup!, SignupAgreements.TermsVersion, SignupAgreements.PrivacyVersion,
                signup.MarketingVersion, keep, lifetime.Token);
        }
        submit.Click += async (_, _) => await SignInAsync(null);
        async Task SignInAsync(SocialClient? client)
        {
            if (!submit.IsEnabled || lifetime.IsCancellationRequested) return;
            var keepSignedIn = remember.IsChecked == true;
            AccountFormElements.SetFeedback(error, "");
            if (client is null && (login.Text.Trim().Length == 0 || password.Password.Length == 0))
            { error.Text = "아이디와 비밀번호를 입력해 주세요."; (login.Text.Trim().Length == 0 ? (Control)login : password).Focus(); return; }
            social.IsEnabled = submit.IsEnabled = remember.IsEnabled = false;
            try
            {
                SessionCredential? previous = null;
                if (credentials is not null && !keepSignedIn)
                {
                    try { previous = credentials.Read(); }
                    catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or System.IO.IOException or UnauthorizedAccessException)
                    { error.Text = "저장된 로그인 정보를 확인하지 못했습니다. 다시 시도해 주세요."; return; }
                }
                // The same choice that stores the refresh token for automatic login asks the server for the 30-day session.
                var keep = credentials is not null && keepSignedIn;
                var account = client is null ? await api.LoginAsync(login.Text.Trim(), password.Password, keep, lifetime.Token)
                    : await SignInSocialAsync(client, keep);
                if (account is null) return;
                AccountFormElements.SetFeedback(error, "");
                password.Clear();
                if (lifetime.IsCancellationRequested)
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                    try { await api.LogoutAsync(timeout.Token); } catch (Exception logout) when (logout is HttpRequestException or SemojumApiException or OperationCanceledException) { }
                    return;
                }
                if (!(allowInstitution ? AutomaticLogin.IsDesktopAllowed(account) : AutomaticLogin.IsEditorAllowed(account)))
                {
                    await api.LogoutAsync(lifetime.Token);
                    error.Text = "이 계정은 에디터를 사용할 수 없습니다."; return;
                }
                if (credentials is not null && keepSignedIn)
                {
                    var storeError = await AutomaticLogin.RememberAsync(api, credentials);
                    if (storeError is not null) { error.Text = storeError; return; }
                }
                else if (credentials is not null && previous?.LoginId == account.LoginId)
                {
                    try { credentials.DeleteIfMatches(previous); }
                    catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or System.IO.IOException or UnauthorizedAccessException)
                    {
                        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                        try { await api.LogoutAsync(timeout.Token); } catch (Exception logout) when (logout is HttpRequestException or SemojumApiException or OperationCanceledException) { }
                        error.Text = "기존 로그인 정보를 삭제하지 못해 로그인을 취소했습니다."; return;
                    }
                }
                if (embeddedResult is { } result) result.TrySetResult(true); else DialogResult = true;
            }
            catch (OperationCanceledException) when (!lifetime.IsCancellationRequested)
            { error.Text = "서버 응답이 지연되었습니다. 연결 상태를 확인하고 다시 시도해 주세요."; }
            catch (OperationCanceledException) { }
            catch (SemojumApiException ex)
            {
                if (ex.Code is "AUTH4001" or "AUTH4006")
                {
                    var rejected = host.LoginRejected(ex); rejected.Owner = ActiveOwner(); rejected.ShowDialog();
                    if (rejected.Choice == 1) OpenEmailForm(false);
                    else { password.Focus(); password.SelectAll(); }
                    return;
                }
                AccountFormElements.SetFeedback(error, ex.Code switch
                {
                    "SOCIAL4002" => "인증 코드가 만료되었거나 사용할 수 없습니다. 소셜 로그인을 다시 시작해 주세요.",
                    "SOCIAL4004" or "SOCIAL4092" => "가입 요청이 만료되었거나 이미 사용되었습니다. 소셜 로그인을 다시 시작해 주세요.",
                    "SOCIAL4005" => "약관 버전이 변경되었습니다. 최신 앱에서 약관을 확인한 뒤 다시 동의해 주세요.",
                    "SOCIAL4091" => SocialConflictMessage(ex),
                    _ => ex.Message
                });
                if (ex.Details is { ValueKind: System.Text.Json.JsonValueKind.Object } d)
                {
                    if (d.TryGetProperty("remainingAttempts", out var remaining)) error.Text += $"\n남은 시도: {remaining}";
                    if (d.TryGetProperty("unlockAt", out var unlock)) error.Text += $"\n다시 시도할 수 있는 시각: {unlock}";
                }
            }
            catch (HttpRequestException) { AccountFormElements.SetFeedback(error, "서버에 연결하지 못했습니다. 인터넷 연결 상태를 확인하고 다시 시도해 주세요."); }
            catch (ArgumentException ex) { error.Text = ex.Message; }
            catch (TimeoutException ex) { AccountFormElements.SetFeedback(error, ex.Message); }
            catch (InvalidOperationException ex) when (client is not null) { AccountFormElements.SetFeedback(error, ex.Message); }
            catch (System.Net.Sockets.SocketException) { AccountFormElements.SetFeedback(error, "로그인 응답을 받을 수 없습니다. 다른 누리닷 로그인 창을 닫고 다시 시도해 주세요."); }
            catch (System.ComponentModel.Win32Exception) { AccountFormElements.SetFeedback(error, "기본 브라우저를 열 수 없습니다. Windows 기본 브라우저 설정을 확인해 주세요."); }
            catch (Exception) { AccountFormElements.SetFeedback(error, "로그인 응답을 처리하지 못했습니다. 다시 시도해 주세요."); }
            finally { social.IsEnabled = submit.IsEnabled = true; remember.IsEnabled = credentials is not null; }
        }
        Loaded += (_, _) => WindowBounds.Fit(this);
        screen.Loaded += async (_, _) => { login.Focus(); await LoadProvidersAsync(); };
    }

    public async Task<bool> ShowEmbeddedAsync(Semojum.UI.Common.IEmbeddedScreenHost host, bool allowCancel = false)
    {
        var owner = host.Window;
        var screen = embeddedScreen ?? throw new InvalidOperationException("로그인 화면을 만들지 못했습니다.");
        if (screen.Parent is not Panel parent) throw new InvalidOperationException("로그인 화면이 이미 사용 중입니다.");
        parent.Children.Remove(screen);
        var completion = embeddedResult = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (allowCancel)
        {
            var back = new Button { Content = "편집기로 돌아가기", HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top, Margin = new(20), Padding = new(12, 4, 12, 4) };
            AutomationProperties.SetAutomationId(back, "EmbeddedLoginCancel");
            back.Click += (_, _) => completion.TrySetResult(false);
            screen.Children.Add(back);
        }
        void OwnerClosed(object? _, EventArgs __) => completion.TrySetResult(false);
        owner.Closed += OwnerClosed;
        if (authCaption.Parent is Border standaloneBar) standaloneBar.Child = null;
        try { return await host.ShowScreenAsync(screen, "누리닷 로그인", completion.Task, authCaption); }
        finally
        {
            owner.Closed -= OwnerClosed;
            embeddedLifetime?.Cancel(); embeddedPassword?.Clear();
            embeddedResult = null;
            Close();
        }
    }

    private static string SocialConflictMessage(SemojumApiException error)
    {
        if (error.Details is not { ValueKind: System.Text.Json.JsonValueKind.Object } details ||
            !details.TryGetProperty("existingLoginMethods", out var methods) || methods.ValueKind != System.Text.Json.JsonValueKind.Array)
            return error.Message;
        var names = methods.EnumerateArray().Where(item => item.ValueKind == System.Text.Json.JsonValueKind.String)
            .Select(item => item.GetString() switch { "PASSWORD" => "아이디와 비밀번호", "GOOGLE" => "Google", "KAKAO" => "카카오", "NAVER" => "네이버", _ => null })
            .Where(name => name is not null).ToArray();
        return names.Length == 0 ? error.Message : $"이 이메일로 가입한 계정이 있습니다. 기존 로그인 방법({string.Join(", ", names)})으로 로그인한 뒤 설정에서 계정을 연결해 주세요.";
    }

    private static Brush Brush(string value) => (Brush)new BrushConverter().ConvertFromString(value)!;
    private static SvgViewbox DesignIcon(string name, double size) => new() { Source = new Uri($"pack://application:,,,/Semojum.Feature.Account.Wpf;component/Assets/Figma/{name}"),
        Width = size, Height = size, Stretch = Stretch.None, IsHitTestVisible = false };
}
