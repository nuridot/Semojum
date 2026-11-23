using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Semojum.Core.Integration;
using Semojum.Feature.Usage.Wpf;

namespace Semojum.Desktop.Dialogs;

public partial class MyPageView : UserControl
{
    private const string Billing = "/api/members/me/billing";
    private readonly MyPageWindow owner;
    private readonly SemojumApiClient api;
    private readonly UsageActions actions;
    private readonly Guid? session;
    private readonly CancellationToken lifetime;
    private readonly bool personal;
    private readonly DateOnly today = UsageDialog.TodayInKorea();
    private readonly List<UsageDialog.BillingRow> usageRows = [];
    private PaymentRow[] payments = [];
    private long? nextMovement;
    private int shownUsage, shownPayments;
    private bool loadingUsage, loadingPayments, usageLoaded, paymentsLoaded;
    private string profileAlias = "";
    private bool marketingEnabled;
    private sealed record PaymentRow(string Date, string Kind, string Description, string Method, string Amount, PersonalInvoice? Invoice)
    { public bool HasInvoice => Invoice is not null; }

    internal MyPageView(MyPageWindow owner, SemojumApiClient api, UsageActions actions, CancellationToken lifetime)
    {
        this.owner = owner;
        this.api = api;
        this.actions = actions;
        this.lifetime = lifetime;
        session = api.SessionMarker;
        personal = api.Account?.MemberType == "PERSONAL";
        InitializeComponent();
        PageBody.SizeChanged += (_, _) => UpdateScrollClearance();
        PageScroll.ScrollChanged += (_, change) =>
        {
            if (change.OriginalSource == PageScroll && change.ViewportHeightChange != 0) UpdateScrollClearance();
        };
        UsageFilter.ItemsSource = personal ? new[] { "전체", "사용", "지급", "예약", "예약 해제", "만료", "조정" } : new[] { "전체", "사용", "충전" };
        UsageFilter.SelectedIndex = 0;
        UsageFilter.SelectionChanged += (_, _) => { if (usageLoaded) DisplayUsage(10); };
        Period.Text = today.ToString("yyyy년 M월", CultureInfo.InvariantCulture);
        SetName(api.Account?.DisplayName ?? "—");
        LoginValue.Text = api.Account?.LoginId ?? "—";
        ConfigureProfile(false, false);
        OrderActions.Visibility = PhotoAction.Visibility = personal ? Visibility.Visible : Visibility.Collapsed;
        if (!personal)
        {
            OwnKind.Text = "이번 달 사용량";
            PasswordValue.Text = EmailValue.Text = LeaveValue.Text = "기관 관리자에게 문의해 주세요.";
            PlanKind.Text = "기관 요금제";
            if (actions.OpenInstitution is not null) BackLabel.Text = "기관 관리로 돌아가기";
        }
        Notifications.IsEnabled = actions.OpenNotifications is not null;
        Loaded += FirstLoad;
    }

    private async void FirstLoad(object sender, RoutedEventArgs e)
    {
        Loaded -= FirstLoad;
        await Task.WhenAll(LoadSummaryAsync(), LoadWalletAsync(), LoadNotificationsAsync(), LoadProfileAsync());
    }

    private void UpdateScrollClearance()
    {
        var margin = PageBody.Margin;
        // A scrolled E2/E4–E6 frame leaves 120 DIP below the last account row.
        // When the whole page fits, keep its standard 24 DIP inset without creating a scrollbar.
        var bottom = PageBody.ActualHeight + margin.Top + 24 > PageScroll.ViewportHeight ? 120 : 24;
        if (margin.Bottom != bottom) PageBody.Margin = new Thickness(margin.Left, margin.Top, margin.Right, bottom);
    }

    private async Task<JsonElement> ReadAsync(string path)
    {
        var value = await api.SendAuthenticatedAsync(HttpMethod.Get, path, cancellationToken: lifetime, expectedSession: session);
        lifetime.ThrowIfCancellationRequested();
        if (session != api.SessionMarker) throw new OperationCanceledException();
        return value;
    }

    private static long? Number(JsonElement data, string key) => data.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long number) ? number : null;
    private static string? String(JsonElement data, string key) => data.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static string Count(long amount) => amount.ToString("#,0", CultureInfo.InvariantCulture);
    private static void Meter(TextBlock value, ProgressBar bar, TextBlock caption, long available, long? total)
    {
        value.Text = total.HasValue ? $"{Count(available)} / {Count(total.Value)}" : Count(available);
        bar.Value = total is > 0 ? Math.Clamp((double)available / total.Value * 100, 0, 100) : 0;
        caption.Text = total is > 0 ? $"{bar.Value:0}% 남음" : $"{Count(available)} 크레딧 사용 가능";
    }

    private async Task LoadSummaryAsync()
    {
        try
        {
            var data = await ReadAsync("/api/users/usage?month=" + today.ToString("yyyy-MM", CultureInfo.InvariantCulture));
            if (!personal && Number(data, "myCredits") is long used)
            {
                OwnValue.Text = Count(used);
                OwnCaption.Text = "이번 달 내가 쓴 크레딧";
                if (Number(data, "orgAllocated") is > 0 and long allocation)
                {
                    OwnBar.Value = Math.Clamp((double)used / allocation * 100, 0, 100);
                    OwnCaption.Text = $"기관 할당량의 {OwnBar.Value:0}% 사용";
                }
                else OwnBar.Visibility = Visibility.Collapsed;
            }
            if (Number(data, "orgRemaining") is long remaining)
                Meter(OrgValue, OrgBar, OrgCaption, remaining, Number(data, "orgAllocated"));
            else { OrgValue.Text = "소속 기관 없음"; OrgBar.Visibility = OrgCaption.Visibility = Visibility.Collapsed; }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { OrgCaption.Text = "사용량을 불러오지 못했습니다."; if (!personal) OwnCaption.Text = OrgCaption.Text; }
    }

    private async Task LoadWalletAsync()
    {
        try
        {
            var data = await ReadAsync("/api/users/credit/wallet");
            if (personal && Number(data, "available") is long available)
            {
                OwnKind.Text = "사용 가능 / 유효 잔액";
                Meter(OwnValue, OwnBar, OwnCaption, available, Number(data, "validBalance"));
            }
            PlanKind.Text = personal ? "개인 요금제" : "기관 요금제";
            if (data.TryGetProperty("subscription", out var subscription) && subscription.ValueKind == JsonValueKind.Object)
            {
                PlanName.Text = String(subscription, "planName") ?? "—";
                if (DateOnly.TryParse(String(subscription, "nextGrantOn"), CultureInfo.InvariantCulture, out var next))
                    Renewal.Text = next.ToString("yyyy. MM. dd.", CultureInfo.InvariantCulture) + " 다음 크레딧 지급";
            }
            else { PlanName.Text = personal ? "구독 없음" : "기관 계약"; Renewal.Text = personal ? "" : "계약 내용은 기관 관리자에게 문의해 주세요."; }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { Renewal.Text = "요금제를 불러오지 못했습니다."; if (personal) OwnCaption.Text = "잔액을 불러오지 못했습니다."; }
    }

    private void SetName(string name)
    {
        HeaderName.Text = Alias.Text = name;
        Initial.Text = name.Length == 0 ? "" : StringInfo.GetNextTextElement(name);
    }

    private void ConfigureProfile(bool loaded, bool password)
    {
        Marketing.IsEnabled = LeaveAction.IsEnabled = PhotoAction.IsEnabled = personal && loaded;
        EmailAction.IsEnabled = PasswordAction.IsEnabled = personal && password;
    }

    private async Task LoadProfileAsync()
    {
        if (!personal) return;
        try
        {
            var data = await ReadAsync("/api/members/me/profile");
            profileAlias = String(data, "alias") ?? "";
            SetName(string.IsNullOrWhiteSpace(profileAlias) ? api.Account?.DisplayName ?? "—" : profileAlias);
            EmailValue.Text = String(data, "email") ?? "—";
            marketingEnabled = data.GetProperty("marketing").GetBoolean();
            Marketing.IsChecked = marketingEnabled;
            bool password = data.GetProperty("passwordLogin").GetBoolean();
            PasswordValue.Text = password ? "비밀번호 로그인" : "소셜 로그인";
            ConfigureProfile(true, password);
            Photo.Fill = null;
            if (data.TryGetProperty("photoPng", out var encoded) && encoded.ValueKind == JsonValueKind.String)
            {
                using var stream = new MemoryStream(encoded.GetBytesFromBase64());
                var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream; image.EndInit(); image.Freeze();
                Photo.Fill = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { ConfigureProfile(false, false); EmailValue.Text = "계정 정보를 불러오지 못했습니다."; }
    }

    private async Task LoadNotificationsAsync()
    {
        if (actions.OpenNotifications is null) return;
        try
        {
            var data = await ReadAsync("/api/board/notifications");
            var unread = Number(data, "unreadCount") ?? 0;
            AlertCount.Text = unread > 99 ? "99+" : Count(unread);
            AlertBadge.Visibility = unread > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (OperationCanceledException) { }
        catch (Exception) { AlertBadge.Visibility = Visibility.Collapsed; }
    }

    private void Back_Click(object sender, RoutedEventArgs e) => owner.Close();
    private void Account_Click(object sender, RoutedEventArgs e)
    {
        var (menu, page, logout) = AccountContextMenu.Create(Account, api.Account, HeaderName.Text);
        menu.HorizontalOffset += 4;
        menu.VerticalOffset = 8;
        void Destination(string label, Action click)
        {
            var item = new MenuItem { Header = label };
            item.Click += (_, _) => click(); page.Items.Add(item);
        }
        Destination("마이페이지 홈", () => PageScroll.ScrollToTop());
        Destination("내 작업 이력", () => owner.OpenHistory(false));
        Destination("문의 내역", () => owner.OpenHistory(true));
        Destination("게시판", () => actions.OpenBoard(owner));
        if (personal) Destination("계정 관리", OpenProfile);
        if (actions.OpenInstitution is not null) Destination("기관 관리", () => { owner.Close(); actions.OpenInstitution(owner); });
        logout.Click += (_, _) => owner.Logout();
        Account.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private async void Notifications_Click(object sender, RoutedEventArgs e)
    {
        actions.OpenNotifications?.Invoke(owner);
        await LoadNotificationsAsync();
    }
    private void Purchase_Click(object sender, RoutedEventArgs e) => OpenPurchase(PurchaseOptionsDialog.Section.TopUp);
    private void Upgrade_Click(object sender, RoutedEventArgs e) => OpenPurchase(PurchaseOptionsDialog.Section.Plan);
    private async void OrderActions_Click(object sender, RoutedEventArgs e)
    {
        if (!personal) return;
        new PersonalBillingDialog(api, PersonalBillingDialog.Section.Orders) { Owner = owner }.ShowDialog();
        await RefreshBillingAsync();
    }
    private async void OpenPurchase(PurchaseOptionsDialog.Section section)
    {
        if (!personal) { actions.Purchase(owner); return; }
        if (new PurchaseOptionsDialog(api, section) { Owner = owner }.ShowDialog() == true)
            await RefreshBillingAsync();
    }
    private async Task RefreshBillingAsync()
    {
        paymentsLoaded = false;
        await Task.WhenAll(LoadWalletAsync(), Payments.IsExpanded ? LoadPaymentsAsync() : Task.CompletedTask);
    }
    private void EmailAction_Click(object sender, RoutedEventArgs e) => ChangeAccount(AccountChangeDialog.Section.Email);
    private void PasswordAction_Click(object sender, RoutedEventArgs e) => ChangeAccount(AccountChangeDialog.Section.Password);
    private void LeaveAction_Click(object sender, RoutedEventArgs e) => ChangeAccount(AccountChangeDialog.Section.Withdrawal);
    private void ChangeAccount(AccountChangeDialog.Section section)
    {
        if (new AccountChangeDialog(api, section) { Owner = owner }.ShowDialog() == true) owner.Logout(true);
    }
    private async void OpenProfile()
    {
        var dialog = new PersonalProfileDialog(api) { Owner = owner };
        if (dialog.ShowDialog() == true && dialog.SignInAgain) owner.Logout(true);
        else await LoadProfileAsync();
    }
    private async void PhotoAction_Click(object sender, RoutedEventArgs e)
    {
        PhotoAction.IsEnabled = false;
        try { if (await PersonalProfileDialog.ChoosePhotoAsync(owner, api, session, lifetime)) await LoadProfileAsync(); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { EmailValue.Text = ex.Message; }
        finally { if (!lifetime.IsCancellationRequested && !string.IsNullOrEmpty(profileAlias)) PhotoAction.IsEnabled = true; }
    }
    private async void Marketing_Click(object sender, RoutedEventArgs e)
    {
        bool desired = Marketing.IsChecked == true;
        Marketing.IsEnabled = false;
        try
        {
            await api.SendAuthenticatedAsync(HttpMethod.Put, "/api/members/me/profile",
                new { alias = profileAlias, marketing = desired, consentVersion = "2026-11-22" }, lifetime, session);
            marketingEnabled = desired;
        }
        catch (OperationCanceledException) { Marketing.IsChecked = marketingEnabled; }
        catch (Exception ex) { Marketing.IsChecked = marketingEnabled; NoticeDialog.Choose(owner, "알림 설정을 저장하지 못했습니다.", ex.Message, ["확인"], error: true); }
        finally { Marketing.IsEnabled = !lifetime.IsCancellationRequested; }
    }

    private async void Usage_Expanded(object sender, RoutedEventArgs e) { if (!usageLoaded) await LoadUsageAsync(false); }
    private async void UsageMore_Click(object sender, RoutedEventArgs e) => await LoadUsageAsync(true);
    private async Task LoadUsageAsync(bool more)
    {
        if (loadingUsage) return;
        loadingUsage = true;
        UsageMore.IsEnabled = false;
        var requestedFilter = UsageFilter.SelectedItem;
        try
        {
            if (!more || shownUsage >= FilteredUsage().Count() && nextMovement.HasValue)
            {
                if (!more) { usageRows.Clear(); shownUsage = 0; nextMovement = null; }
                if (personal)
                {
                    var page = await ReadAsync(Billing + "/transactions" + (nextMovement.HasValue ? "?before=" + nextMovement.Value.ToString(CultureInfo.InvariantCulture) : ""));
                    var rows = page.EnumerateArray().ToArray();
                    var movements = rows.Select(Movement).ToArray();
                    var from = today.AddDays(-29).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    var through = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    usageRows.AddRange(movements.Where(row => string.CompareOrdinal(row.Date, from) >= 0 && string.CompareOrdinal(row.Date, through) <= 0));
                    nextMovement = rows.Length == 100 ? Number(rows[^1], "id") : null;
                }
                else
                {
                    var from = today.AddDays(-29);
                    var result = await ReadAsync(FormattableString.Invariant($"/api/users/usage/credit-history?from={from:yyyy-MM-dd}&to={today:yyyy-MM-dd}&type=ALL"));
                    usageRows.AddRange(result.GetProperty("items").EnumerateArray().Select(UsageDialog.ReadBillingRow));
                }
            }
            DisplayUsage(more && Equals(requestedFilter, UsageFilter.SelectedItem) ? shownUsage + 10 : 10);
            UsagePeriod.Text = $"최근 30일 · {Count(usageRows.Count)}{(nextMovement.HasValue ? "+" : "")}건";
            usageLoaded = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception) { UsageMessage.Text = "상세 내역을 불러오지 못했습니다. 다시 펼쳐 주세요."; }
        finally { loadingUsage = false; UsageMore.IsEnabled = true; }
    }

    private IEnumerable<UsageDialog.BillingRow> FilteredUsage() => UsageFilter.SelectedItem is string kind && kind != "전체"
        ? usageRows.Where(row => row.Kind == kind) : usageRows;

    private void DisplayUsage(int limit)
    {
        var matches = FilteredUsage().ToArray();
        shownUsage = Math.Min(limit, matches.Length);
        UsageRows.ItemsSource = matches.Take(shownUsage).ToArray();
        UsageMore.Visibility = shownUsage < matches.Length || nextMovement.HasValue ? Visibility.Visible : Visibility.Collapsed;
        UsageMessage.Text = matches.Length > 0 ? "" : nextMovement.HasValue ? "불러온 내역에 해당 항목이 없습니다. 더 보기를 눌러 주세요." : "최근 30일에 해당하는 내역이 없습니다.";
    }
    private static UsageDialog.BillingRow Movement(JsonElement item)
    {
        var kind = String(item, "entryType") switch { "GRANT" => "지급", "CAPTURE" => "사용", "RESERVE" => "예약", "RELEASE" => "예약 해제", "EXPIRE" => "만료", "ADJUST" => "조정", var other => other ?? "—" };
        var date = TimeZoneInfo.ConvertTime(item.GetProperty("createdAt").GetDateTimeOffset(), TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time"));
        return new(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), kind, String(item, "source") ?? "—", "—",
            (Number(item, "balanceDelta") ?? 0).ToString("+#,0;-#,0;0", CultureInfo.InvariantCulture),
            Number(item, "balanceAfter") is long left ? Count(left) : "—");
    }
    private async void Payments_Expanded(object sender, RoutedEventArgs e) { if (!paymentsLoaded) await LoadPaymentsAsync(); }
    private void PaymentsMore_Click(object sender, RoutedEventArgs e) => ShowMorePayments();
    private async Task LoadPaymentsAsync()
    {
        if (loadingPayments) return;
        loadingPayments = true;
        try
        {
            if (!personal)
            {
                PaymentsMessage.Text = "기관 결제 내역은 기관 관리자에게 문의해 주세요.";
                PaymentsRows.Visibility = Visibility.Collapsed;
                paymentsLoaded = true;
                return;
            }
            var data = await ReadAsync(Billing + "/orders");
            payments = data.EnumerateArray().Select(Payment).ToArray();
            PaymentsPeriod.Text = $"최근 12개월 · {Count(payments.Length)}건";
            PaymentsMessage.Text = payments.Length == 0 ? "최근 12개월 결제 내역이 없습니다." : "";
            shownPayments = 0;
            ShowMorePayments();
            paymentsLoaded = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception) { PaymentsMessage.Text = "결제 내역을 불러오지 못했습니다. 다시 펼쳐 주세요."; }
        finally { loadingPayments = false; }
    }
    private static PaymentRow Payment(JsonElement item)
    {
        var date = TimeZoneInfo.ConvertTime(item.GetProperty("createdAt").GetDateTimeOffset(), TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time"));
        var kind = String(item, "kind") == "PLAN" ? "요금제" : "추가 크레딧";
        var state = String(item, "status") switch { "DRAFT" => "주문안", "CANCELLED" => "취소", "PAID" => "결제 완료", "REFUNDED" => "환불", var value => value };
        var itemName = String(item, "planCode") is { } code ? $"{code} 요금제" : Count(Number(item, "credits") ?? 0) + " 크레딧";
        var amount = Count(item.GetProperty("amountKrw").GetInt64()) + "원";
        PersonalInvoice? invoice = null;
        if (item.TryGetProperty("hasInvoice", out var issued) && issued.ValueKind == JsonValueKind.True
            && item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String && id.TryGetGuid(out var orderId))
        {
            string? paidDate = item.TryGetProperty("paidAt", out var paid) && paid.ValueKind == JsonValueKind.String && paid.TryGetDateTimeOffset(out var instant)
                ? TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time")).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
            invoice = new(orderId, paidDate, itemName, amount);
        }
        return new(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), kind, $"{itemName} · {state}", "—", amount, invoice);
    }
    private void Invoice_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: PaymentRow { Invoice: { } invoice } })
            new PersonalInvoiceDialog(api, invoice) { Owner = owner }.ShowDialog();
    }
    private void ShowMorePayments()
    {
        shownPayments = Math.Min(shownPayments + 10, payments.Length);
        PaymentsRows.ItemsSource = payments.Take(shownPayments).ToArray();
        PaymentsMore.Visibility = shownPayments < payments.Length ? Visibility.Visible : Visibility.Collapsed;
    }
}
