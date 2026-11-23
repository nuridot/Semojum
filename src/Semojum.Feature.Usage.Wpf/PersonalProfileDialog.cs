using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Semojum.Core.Integration;

namespace Semojum.Desktop.Dialogs;

public sealed class PersonalProfileDialog : CompactDialog
{
    private const string Me = "/api/members/me";
    private readonly SemojumApiClient api;
    private readonly Guid? marker;
    private readonly TextBox alias = new() { MaxLength = 50 };
    private readonly CheckBox marketing = new() { Content = "광고성 정보 이메일 수신" };
    private readonly Image photo = new() { Width = 72, Height = 72, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock address = new();
    public bool SignInAgain { get; private set; }

    public PersonalProfileDialog(SemojumApiClient api) : base("개인 계정 관리", 580, 600)
    {
        this.api = api;
        marker = api.SessionMarker;
        var profile = new StackPanel { Margin = new(24) };
        Body(new ScrollViewer { Content = profile, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Button Do(string label, Func<Task> task) => Button(label, () => RunAsync(task));
        profile.Children.Add(photo);
        profile.Children.Add(Do("프로필 사진 변경", ChangePhotoAsync));
        Row(profile, "계정명", alias); Row(profile, "이메일", address);
        profile.Children.Add(marketing);
        profile.Children.Add(Do("변경 저장", async () =>
        {
            await Send(HttpMethod.Put, "/profile", new { alias = alias.Text.Trim(), marketing = marketing.IsChecked == true, consentVersion = "2026-11-22" });
            await RefreshAsync(); Status.Text = "프로필을 저장했습니다.";
        }));

        void OpenChange(AccountChangeDialog.Section section)
        {
            if (new AccountChangeDialog(api, section) { Owner = this }.ShowDialog() == true)
            { SignInAgain = true; DialogResult = true; }
        }
        var email = Button("이메일 변경", () => { OpenChange(AccountChangeDialog.Section.Email); return Task.CompletedTask; });
        var password = Button("비밀번호 변경", () => { OpenChange(AccountChangeDialog.Section.Password); return Task.CompletedTask; });
        profile.Children.Add(email); profile.Children.Add(password);
        profile.Children.Add(Button("회원 탈퇴", () => { OpenChange(AccountChangeDialog.Section.Withdrawal); return Task.CompletedTask; }));
        email.IsEnabled = password.IsEnabled = false;
        OnLoad(async () => email.IsEnabled = password.IsEnabled = (await RefreshAsync()).GetProperty("passwordLogin").GetBoolean());
        Action("닫기", Close);
    }

    private Task<JsonElement> Send(HttpMethod method, string path, object? body = null) =>
        api.SendAuthenticatedAsync(method, Me + path, body, Lifetime.Token, marker);


    private async Task<JsonElement> RefreshAsync()
    {
        var data = await Send(HttpMethod.Get, "/profile");
        alias.Text = data.GetProperty("alias").GetString() ?? "";
        address.Text = data.GetProperty("email").GetString() ?? "";
        marketing.IsChecked = data.GetProperty("marketing").GetBoolean();
        photo.Source = null;
        if (data.TryGetProperty("photoPng", out var png) && png.ValueKind == JsonValueKind.String)
        {
            using var stream = new MemoryStream(png.GetBytesFromBase64());
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream; image.EndInit(); image.Freeze(); photo.Source = image;
        }
        return data;
    }

    private async Task ChangePhotoAsync()
    {
        if (await ChoosePhotoAsync(this, api, marker, Lifetime.Token)) await RefreshAsync();
    }

    internal static async Task<bool> ChoosePhotoAsync(Window owner, SemojumApiClient client, Guid? session, CancellationToken token)
    {
        var open = new OpenFileDialog { Filter = "프로필 사진|*.png;*.jpg;*.jpeg" };
        if (open.ShowDialog(owner) != true) return false;
        if (new FileInfo(open.FileName).Length > 1024 * 1024) throw new ArgumentException("1 MB 이하의 PNG 또는 JPEG를 선택해 주세요.");
        var bytes = await File.ReadAllBytesAsync(open.FileName, token);
        await client.SendAuthenticatedAsync(HttpMethod.Put, Me + "/profile/photo", new { imageBase64 = Convert.ToBase64String(bytes) }, token, session);
        return true;
    }
}
