using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Semojum.Core.Integration;

namespace Semojum.Desktop.Dialogs;

public sealed class AccountChangeDialog : MyPageActionDialog
{
    public enum Section { Email, Password, Withdrawal }
    private readonly SemojumApiClient api;
    private readonly Guid? marker;
    private readonly TextBlock address = new() { FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
    private readonly PasswordBox currentPassword = new();
    private readonly StackPanel rows = new();
    private readonly Button submit;
    private bool hasPassword;

    public AccountChangeDialog(SemojumApiClient api, Section section)
        : base(section switch { Section.Email => "이메일 변경", Section.Password => "비밀번호 변경", _ => "회원 탈퇴" },
            520, section switch { Section.Email => 262, Section.Password => 274, _ => 278 })
    {
        this.api = api; marker = api.SessionMarker;
        CancelButton();
        submit = section switch { Section.Email => EmailForm(), Section.Password => PasswordForm(), _ => WithdrawalForm() };
        Fields.IsEnabled = submit.IsEnabled = false;
        OnLoad(async () =>
        {
            var profile = await Send(HttpMethod.Get, "/profile");
            address.Text = profile.GetProperty("email").GetString();
            hasPassword = profile.GetProperty("passwordLogin").GetBoolean();
            if (section == Section.Withdrawal && !hasPassword)
            {
                rows.Visibility = Visibility.Collapsed;
                if (rows.Parent is Border card) card.Visibility = Visibility.Collapsed;
            }
            var eligible = hasPassword || section == Section.Withdrawal;
            Fields.IsEnabled = submit.IsEnabled = eligible;
            if (!eligible) Status.Text = "비밀번호로 로그인하는 계정에서 사용할 수 있습니다.";
        });
    }

    private Task<JsonElement> Send(HttpMethod method, string path, object? payload = null) =>
        api.SendAuthenticatedAsync(method, "/api/members/me" + path, payload, Lifetime.Token, marker);

    private Button EmailForm()
    {
        var next = new TextBox();
        var code = new TextBox { MaxLength = 6 };
        string? challenge = null;
        var addressRow = new Border { Height = 20, Child = address };
        FormRow(rows, "지금 이메일", addressRow, 122);
        FormRow(rows, "새 이메일", Input(next, "새 이메일", "새 이메일 주소"), 122);
        var codeRow = new DockPanel();
        var request = Button("인증 번호 받기", () => RunAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(next.Text)) throw new ArgumentException("새 이메일 주소를 입력해 주세요.");
            var password = new EmailPasswordDialog { Owner = this };
            if (password.ShowDialog() != true) return;
            try
            {
                challenge = null; code.Clear();
                var result = await Send(HttpMethod.Post, "/email/request", new { email = next.Text.Trim(), currentPassword = password.Field.Password });
                challenge = result.GetProperty("verificationToken").GetString();
                Status.Text = $"새 이메일로 보낸 인증 번호를 {Math.Ceiling(result.GetProperty("expiresInSeconds").GetInt32() / 60.0):N0}분 안에 입력해 주세요.";
            }
            finally { password.Field.Clear(); }
            _ = Dispatcher.BeginInvoke(() => code.Focus(), DispatcherPriority.Input);
        }));
        request.Width = 103; request.Margin = new(12, 0, 0, 0); request.Template = EditorModalDesign.ButtonTemplate(typeof(Button));
        DockPanel.SetDock(request, Dock.Right); codeRow.Children.Add(request); codeRow.Children.Add(Input(code, "인증 번호", "6자리"));
        FormRow(rows, "인증 번호", codeRow, 122);
        Fields.Children.Add(Card(rows, new(16, 12, 16, 12)));
        next.TextChanged += (_, _) => { challenge = null; code.Clear(); Status.Text = ""; };
        return FooterButton("변경", async () =>
        {
            if (challenge is null || code.Text.Length != 6 || !code.Text.All(char.IsAsciiDigit))
                throw new ArgumentException("새 이메일로 받은 6자리 인증 번호를 입력해 주세요.");
            await Send(HttpMethod.Post, "/email/confirm", new { verificationToken = challenge, code = code.Text });
            DialogResult = true;
        }, true);
    }

    private Button PasswordForm()
    {
        var next = new PasswordBox(); var confirmation = new PasswordBox();
        FormRow(rows, "지금 비밀번호", Input(currentPassword, "지금 비밀번호"), 132);
        FormRow(rows, "새 비밀번호", Input(next, "새 비밀번호", "8자 이상"), 132);
        FormRow(rows, "새 비밀번호 확인", Input(confirmation, "새 비밀번호 확인"), 132);
        Fields.Children.Add(Card(rows, new(16, 12, 16, 12)));
        return FooterButton("변경", async () =>
        {
            if (next.Password.Length == 0 || next.Password != confirmation.Password) throw new ArgumentException("새 비밀번호와 확인 값이 일치해야 합니다.");
            await Send(HttpMethod.Post, "/password", new { currentPassword = currentPassword.Password, newPassword = next.Password });
            DialogResult = true;
        }, true);
    }

    private Button WithdrawalForm()
    {
        Fields.Children.Add(new TextBlock { Text = "탈퇴하면 계정명과 이메일, 프로필 사진, 소셜 연결이 삭제됩니다. 작업·크레딧·동의 기록은 비식별 계정에 남습니다.",
            TextWrapping = TextWrapping.Wrap, FontSize = 14, LineHeight = 20, Margin = new(0, 0, 0, 16) });
        FormRow(rows, "비밀번호", Input(currentPassword, "비밀번호", "비밀번호 입력"), 108);
        Fields.Children.Add(Card(rows, new(16, 12, 16, 12)));
        var consent = new CheckBox { Content = "위 내용을 확인했습니다", FontSize = 14, VerticalContentAlignment = VerticalAlignment.Center, Margin = new(0, 16, 0, 0) };
        Fields.Children.Add(consent);
        return FooterButton("탈퇴", async () =>
        {
            if (consent.IsChecked != true) throw new ArgumentException("탈퇴 안내를 확인해 주세요.");
            if (NoticeDialog.Choose(this, "개인 계정을 탈퇴할까요?", "확인하면 로그인 세션이 종료됩니다.", ["탈퇴", "취소"], error: true) != 0) return;
            await Send(HttpMethod.Post, "/withdraw", new { currentPassword = hasPassword ? currentPassword.Password : "" });
            DialogResult = true;
        }, true);
    }

    private sealed class EmailPasswordDialog : MyPageActionDialog
    {
        public PasswordBox Field { get; } = new();
        public EmailPasswordDialog() : base("이메일 변경 본인 확인", 420, 190)
        {
            var row = new StackPanel();
            FormRow(row, "지금 비밀번호", Input(Field, "지금 비밀번호"), 112);
            Fields.Children.Add(Card(row, new(16, 12, 16, 12))); CancelButton();
            FooterButton("확인", () =>
            {
                if (string.IsNullOrEmpty(Field.Password)) throw new ArgumentException("지금 비밀번호를 입력해 주세요.");
                DialogResult = true; return Task.CompletedTask;
            }, true);
            ContentRendered += (_, _) => Field.Focus();
        }
    }
}
