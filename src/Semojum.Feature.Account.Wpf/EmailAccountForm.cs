using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Semojum.Core.Integration;


namespace Semojum.Desktop.Dialogs;

internal sealed class EmailAccountForm : Border
{
    private readonly SemojumApiClient api;
    private readonly bool signup;
    private readonly StackPanel fields = new();
    private readonly Dictionary<Control, FrameworkElement> fieldViews = new();
    private readonly TextBox email = new();
    private readonly PasswordBox password = new(), confirmation = new();
    private readonly TextBox code = new() { MaxLength = 6 };
    private readonly CheckBox terms = new(), privacy = new(), marketing = new();
    private readonly TextBlock feedback = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Firebrick };
    private readonly Button submit = AccountFormElements.ActionButton("회원가입", true);
    private readonly Button resend = AccountFormElements.ActionButton("인증 번호 재발송", false);
    private readonly CancellationTokenSource lifetime = new();
    private readonly DispatcherTimer clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Action<bool, string, string?> finish;
    private EmailChallenge? challenge;
    private DateTimeOffset expires, canResend;
    private bool busy;

    internal EmailAccountForm(SemojumApiClient api, bool signup, string initialEmail, Action<bool, string, string?> finish)
    {
        this.api = api; this.signup = signup; this.finish = finish;
        AccountFormElements.StyleCard(this);
        AutomationProperties.SetAutomationId(this, signup ? "SignupCard" : "ResetCard");
        Margin = new(12,16,12,16); HorizontalAlignment = HorizontalAlignment.Center; VerticalAlignment = VerticalAlignment.Center;
        email.Text = initialEmail;
        var card = new StackPanel(); card.Children.Add(AccountFormElements.Logo());
        AccountFormElements.StyleFeedback(feedback); submit.IsDefault = true; submit.Margin = new(0,16,0,0);
        resend.Margin = new(0,8,0,0);
        card.Children.Add(fields); card.Children.Add(feedback); card.Children.Add(submit); Child = card;
        submit.Click += async (_, _) => await RunAsync(false);
        resend.Click += async (_, _) => await RunAsync(true);
        clock.Tick += (_, _) => RefreshActions();
        Unloaded += (_, _) => { clock.Stop(); lifetime.Cancel(); password.Clear(); confirmation.Clear(); };
        Draw(); Loaded += (_, _) => email.Focus();
    }

    internal void GoBack()
    {
        lifetime.Cancel();
        finish(false, email.Text, null);
    }

    private void AddField(string label, Control control)
    {
        if (!fieldViews.TryGetValue(control, out var field))
            fieldViews.Add(control, field = AccountFormElements.Field(label, control));
        fields.Children.Add(field);
    }

    private void Draw()
    {
        fields.Children.Clear();
        AddField("이메일", email); email.IsReadOnly = challenge is not null;
        if (signup && challenge is null || !signup && challenge is not null)
        {
            AddField(signup ? "비밀번호" : "새 비밀번호", password);
            AddField("비밀번호 확인", confirmation);
        }
        if (signup && challenge is null)
        {
            var agreements = new StackPanel();
            var checkStyle = AccountFormElements.CheckStyle();
            var all = new CheckBox { Content = "약관에 모두 동의합니다", Style = checkStyle };
            agreements.Children.Add(all);
            void Agreement(CheckBox check, string label, LegalDocumentKind kind)
            {
                var row = new DockPanel { Margin = new(0, 8, 0, 0) };
                var view = new Button { Content = "보기", FontSize = 12, Height = 20, MinWidth = 0, MinHeight = 0, Padding = new(0), Margin = new(0),
                    Background = Brushes.Transparent, BorderBrush = Brushes.Transparent, Foreground = AccountFormElements.Ink("#003E92"), Template = FigmaDialogElements.SurfaceTemplate(0) };
                AutomationProperties.SetName(view, label + " 보기");
                void SyncAll() => all.IsChecked = terms.IsChecked == true && privacy.IsChecked == true && marketing.IsChecked == true;
                view.Click += (_, _) =>
                {
                    var dialog = new LegalDocumentDialog(kind) { Owner = Window.GetWindow(this) };
                    dialog.ShowDialog();
                    if (dialog.MarketingChoice is { } choice) { check.IsChecked = choice; SyncAll(); }
                };
                DockPanel.SetDock(view, Dock.Right); row.Children.Add(view);
                check.Content = label; check.Style = checkStyle; row.Children.Add(check); agreements.Children.Add(row);
                check.Click += (_, _) => SyncAll();
            }
            Agreement(terms, "[필수] 이용약관 동의", LegalDocumentKind.Terms);
            Agreement(privacy, "[필수] 개인정보 처리방침 동의", LegalDocumentKind.Privacy);
            Agreement(marketing, "[선택] 광고성 정보 수신 동의", LegalDocumentKind.Marketing);
            all.Click += (_, _) => terms.IsChecked = privacy.IsChecked = marketing.IsChecked = all.IsChecked;
            fields.Children.Add(new Border { Background = AccountFormElements.Ink("#F9F9F9"), CornerRadius = new(4), Margin = new(0,16,0,0), Padding = new(12), Child = agreements });
        }
        if (challenge is not null)
        {
            AddField("이메일 인증 번호 6자리", code);
            fields.Children.Add(resend); Dispatcher.InvokeAsync(() => { if (code.IsVisible) code.Focus(); }, DispatcherPriority.Input);
        }
        submit.Content = challenge is null ? signup ? "회원가입" : "인증 번호 받기" : signup ? "인증하고 가입" : "비밀번호 재설정";
        RefreshActions();
    }

    private void RefreshActions()
    {
        var now = DateTimeOffset.UtcNow;
        resend.IsEnabled = !busy && now >= canResend;
        resend.Content = now >= canResend ? "인증 번호 재발송" : $"인증 번호 재발송 ({Math.Ceiling((canResend - now).TotalSeconds)}초)";
        submit.IsEnabled = !busy && (challenge is null || now < expires);
        if (challenge is not null && now >= expires) AccountFormElements.SetFeedback(feedback, "인증 번호가 만료되었습니다. 다시 발송해 주세요.");
    }

    private async Task RunAsync(bool sendAgain)
    {
        if (busy || lifetime.IsCancellationRequested) return;
        busy = true; fields.IsEnabled = false; RefreshActions(); feedback.Text = "";
        try
        {
            var address = email.Text.Trim();
            if (address.Length == 0) throw new ArgumentException("이메일을 입력해 주세요.");
            var needsPassword = signup ? challenge is null : challenge is not null;
            if (!sendAgain && needsPassword && (password.Password.Length == 0 || password.Password != confirmation.Password))
                throw new ArgumentException("비밀번호와 비밀번호 확인을 다시 입력해 주세요.");
            if (signup && (terms.IsChecked != true || privacy.IsChecked != true)) throw new ArgumentException("필수 약관에 동의해 주세요.");
            if (challenge is null || sendAgain)
            {
                EmailChallenge next;
                if (sendAgain) next = await api.ResendEmailCodeAsync(signup, challenge!.VerificationToken, lifetime.Token);
                else if (signup) next = await api.StartSignupAsync(address, password.Password, "2026-11-09", "2026-11-09",
                    marketing.IsChecked == true ? "2026-11-22" : null, lifetime.Token);
                else next = await api.StartPasswordResetAsync(address, lifetime.Token);
                if (lifetime.IsCancellationRequested) return;
                var first = challenge is null;
                challenge = next; expires = DateTimeOffset.UtcNow.AddSeconds(next.ExpiresInSeconds);
                canResend = DateTimeOffset.UtcNow.AddSeconds(next.ResendAfterSeconds);
                if (signup) { password.Clear(); confirmation.Clear(); }
                code.Clear(); if (first) Draw(); clock.Start();
                AccountFormElements.SetFeedback(feedback, "이메일을 확인해 주세요.", information: true);
                return;
            }
            var pin = code.Text.Trim();
            if (pin.Length != 6 || !pin.All(char.IsAsciiDigit)) throw new ArgumentException("인증 번호 6자리를 입력해 주세요.");
            if (signup) await api.VerifySignupAsync(address, challenge.VerificationToken, pin, lifetime.Token);
            else await api.ConfirmPasswordResetAsync(challenge.VerificationToken, pin, password.Password, lifetime.Token);
            if (!lifetime.IsCancellationRequested) finish(signup, address, signup ? null : "비밀번호가 변경되었습니다. 새 비밀번호로 로그인해 주세요.");
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex) when (ex is SemojumApiException or HttpRequestException or OperationCanceledException or ArgumentException or System.IO.InvalidDataException or JsonException)
        {
            AccountFormElements.SetFeedback(feedback, ex.Message);
            if (ex is SemojumApiException { Details: { ValueKind: JsonValueKind.Object } details }
                && details.TryGetProperty("remainingAttempts", out var left) && left.TryGetInt32(out var count))
                feedback.Text += $" (남은 시도 {count}회)";
        }
        finally { busy = false; fields.IsEnabled = true; RefreshActions(); }
    }
}
