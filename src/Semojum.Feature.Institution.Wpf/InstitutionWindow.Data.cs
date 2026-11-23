using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Semojum.Desktop.Services;

namespace Semojum.Desktop.Dialogs;

public sealed partial class InstitutionWindow
{
    private InstitutionClient Client => new(api, institutionSessionMarker);
    private bool refreshing;
    internal bool Refreshing => refreshing;
    private string? receiptEmail;
    private void ClearInstitutionData()
    {
        AccountRequest.IsEnabled = CreditRequest.IsEnabled = false;
        Identity.Text = "정보 없음"; Identity.ToolTip = null; Allocated.Text = Balance.Text = Contract.Text = "—"; CreditDetail.Text = "사용량 정보 없음"; UsagePercent.Text = "정보 없음";
        ContractRemaining.Text = "정보 없음"; UsageBar.Value = 0; UsageBar.Visibility = Visibility.Collapsed;
        RenderLayoutUsage(null); RenderAccountUsage(null);
        RenderMonthlyUsage([]); Accounts.ItemsSource = Notices.ItemsSource = Orders.ItemsSource = null; PendingRequests.Text = ""; ReceiptAddress.IsEnabled = false; receiptEmail = null; ReceiptAddress.Text = ""; ReceiptHint.Visibility = Visibility.Collapsed;
    }
    private async Task RefreshAsync()
    {
        if (refreshing) return;
        refreshing = true; ErrorText.Text = "";
        ClearInstitutionData();
        var errors = new List<string>();
        async Task Load(string label, Func<Task> load)
        {
            try { await load(); }
            catch (OperationCanceledException) when (Lifetime.IsCancellationRequested) { }
            catch (Exception ex) { errors.Add(label + ": " + ex.Message); }
        }
        try
        {
            await Load("기관 정보", async () =>
            {
                var value = await Client.DashboardAsync(Lifetime.Token);
                Identity.Text = value.OrgName + " · " + api.Account?.LoginId; Identity.ToolTip = value.OrgCode;
                Allocated.Text = $"{value.CreditUsed:N0} / {value.CreditAllocated:N0}"; Balance.Text = value.CreditRemaining.ToString("N0");
                CreditDetail.Text = Forecast(value);
                var usage = InstitutionUsage.UsageRatio(value.CreditAllocated, value.CreditUsed);
                UsagePercent.Text = "사용률 " + usage; UsageBar.Visibility = usage == "정보 없음" ? Visibility.Collapsed : Visibility.Visible;
                UsageBar.Value = value.CreditAllocated > 0 && value.CreditUsed >= 0 ? Math.Clamp(100d * value.CreditUsed / value.CreditAllocated, 0, 100) : 0;
                Contract.Text = (ValidDate(value.ContractStartedAt) ? value.ContractStartedAt : "—") + " ~ " + (ValidDate(value.ContractExpiresAt) ? value.ContractExpiresAt : "—");
                var remaining = InstitutionUsage.RemainingDays(value.ContractExpiresAt, TodayInKorea());
                ContractRemaining.Text = remaining.EndsWith(" 남음", StringComparison.Ordinal) ? "남은 기간 " + remaining[..^3] : remaining;
                RenderMonthlyUsage(value.MonthlyUsage); RenderLayoutUsage(value.LayoutUsage);
                AccountRequest.IsEnabled = CreditRequest.IsEnabled = true; AccountRequest.ToolTip = CreditRequest.ToolTip = "운영자에게 요청을 보냅니다.";
            });
            InstitutionAccountRow[] members = [];
            await Load("소속 계정", async () =>
            {
                Accounts.ItemsSource = members = (await Client.AccountsAsync(Lifetime.Token)).Items.Select(a => InstitutionAccountRow.Of(a)).ToArray(); RenderAccountUsage(members);
            });
            // Open account-issue requests are lines of the account table; there is no separate request history.
            await Load("발급 요청", async () =>
            {
                var issues = (await Client.RequestsAsync(Lifetime.Token)).Where(r => r is { Type: "ACCOUNT_ISSUE", Status: "OPEN" }).Select(r => InstitutionAccountRow.Of(r)).ToArray();
                Accounts.ItemsSource = members.Concat(issues).ToArray(); PendingRequests.Text = issues.Length == 0 ? "" : $"발급 요청 {issues.Length:N0}건 처리 중";
            });
            await Load("공지", async () => Notices.ItemsSource = await Client.NoticesAsync(Lifetime.Token));
            await Load("주문 내역", async () =>
            {
                var orders = await Client.OrdersAsync(Lifetime.Token); Orders.ItemsSource = orders.Items; receiptEmail = orders.ReceiptEmail;
                ReceiptAddress.Text = receiptEmail ?? ""; ReceiptAddress.IsEnabled = true;
            });
            if (api.Account?.Role != "ROLE_ORG_ADMIN")
            {
                ClearInstitutionData();
                ErrorText.Text = "기관 관리자 권한을 다시 확인해야 합니다. 로그아웃 후 다시 로그인해 주세요.";
            }
            else if (errors.Count > 0) ErrorText.Text = string.Join("\n", errors);
        }
        finally { refreshing = false; }
    }
    internal void RenderMonthlyUsage(IReadOnlyList<InstitutionMonthlyUsage> months)
    {
        Chart.Children.Clear();
        var usable = months.Count > 0 && months.All(m => m.Credits >= 0);
        ChartArea.Visibility = usable ? Visibility.Visible : Visibility.Collapsed;
        ChartEmpty.Text = months.Count == 0 ? "월별 사용량 정보 없음" : "월별 사용량을 표시할 수 없습니다.";
        ChartEmpty.Visibility = usable ? Visibility.Collapsed : Visibility.Visible;
        var average = InstitutionUsage.SixMonthAverage(months);
        ChartAverage.Text = average.HasValue ? $"6개월 평균 {average.Value:N0}" : "6개월 평균 정보 없음";
        ChartAverage.ToolTip = ChartAverage.Text;
        if (!usable) { foreach (var tick in ChartTicks) tick.Text = ""; return; }
        var peak = months.Max(m => m.Credits);
        decimal magnitude = 1;
        while (magnitude * 10 < (decimal)peak / 5) magnitude *= 10;
        var interval = new decimal[] { 1, 2, 5, 10 }.First(x => x * magnitude >= (decimal)peak / 5) * magnitude;
        var maximum = interval * 5;
        for (int i = 0; i < ChartTicks.Length; i++) { ChartTicks[i].Text = ((5 - i) * interval).ToString("N0"); ChartTicks[i].ToolTip = ChartTicks[i].Text; }
        foreach (var month in months.OrderBy(m => m.Month, StringComparer.Ordinal))
        {
            var column = new Grid { Width = 52, Height = 140 };
            column.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) }); column.RowDefinitions.Add(new() { Height = new(20) });
            var bar = new Border { Width = 24, Height = (double)(104m * month.Credits / maximum), CornerRadius = new(3,3,0,0), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, ToolTip = month.Month + " · " + month.Credits.ToString("N0") };
            System.Windows.Automation.AutomationProperties.SetName(bar, month.Month + " 사용 크레딧 " + month.Credits.ToString("N0")); column.Children.Add(bar);
            var amount = new TextBlock { Text = month.Credits.ToString("N0"), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = month.Credits.ToString("N0"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new(0, 0, 0, bar.Height + 4) }; column.Children.Add(amount);
            var monthLabel = DateTime.TryParseExact(month.Month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date.Month + "월" : month.Month;
            var label = new TextBlock { Text = monthLabel, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = month.Month, Margin = new(0,4,0,0), HorizontalAlignment = HorizontalAlignment.Center }; Grid.SetRow(label, 1); column.Children.Add(label); Chart.Children.Add(column);
        }
        SizeChartColumns(); ApplyChartContrast(chartHighContrast);
    }
    internal void ApplyChartContrast(bool highContrast)
    {
        chartHighContrast = highContrast;
        if (chartCard is not null)
        {
            chartCard.Background = highContrast ? SystemColors.WindowBrush : System.Windows.Media.Brushes.White;
            chartCard.BorderBrush = highContrast ? SystemColors.WindowTextBrush : FigmaDialogElements.Brush("#EAEAEA");
            if (chartCard.Child is StackPanel panel && panel.Children[0] is DockPanel header)
                foreach (var title in header.Children.OfType<TextBlock>()) title.Foreground = highContrast ? SystemColors.WindowTextBrush : FigmaDialogElements.Brush("#1A1A1A");
        }
        var text = highContrast ? SystemColors.WindowTextBrush : FigmaDialogElements.Brush("#5F5F5F");
        var axis = highContrast ? SystemColors.WindowTextBrush : FigmaDialogElements.Brush("#8A8A8A");
        foreach (var tick in ChartTicks) tick.Foreground = axis;
        ChartAverage.Foreground = ChartEmpty.Foreground = text;
        foreach (var column in Chart.Children.OfType<Grid>())
        foreach (var child in column.Children)
        {
            if (child is Border bar) bar.Background = highContrast ? SystemColors.HighlightBrush : FigmaDialogElements.Brush(column == Chart.Children[Chart.Children.Count - 1] ? "#005FB8" : "#B9D3EE");
            else if (child is TextBlock label) label.Foreground = text;
        }
    }
    private void SizeChartColumns()
    {
        if (Chart.Children.Count == 0) return;
        var available = ChartScroll.ActualWidth > 0 ? ChartScroll.ActualWidth : ChartScroll.ViewportWidth;
        var width = Math.Max(42, Math.Floor(available / Chart.Children.Count));
        foreach (var column in Chart.Children.OfType<FrameworkElement>()) column.Width = width;
    }
    private static bool ValidDate(string? date) => DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    private static DateOnly TodayInKorea() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time")).DateTime);
    private async Task RequestAccountAsync() => await RequestAsync(true);
    private async Task RequestCreditAsync() => await RequestAsync(false);
    private async Task RequestAsync(bool account)
    {
        var dialog = new InstitutionRequestDialog(account, async (message, details) => { await Client.CreateRequestAsync(account ? "ACCOUNT_ISSUE" : "CREDIT_ADD", message, details, Lifetime.Token); },
            account ? null : (credits, ct) => Client.CreditEstimateAsync(credits, ct)) { Owner = ModalOwner };
        if (dialog.ShowDialog() == true) await RefreshAsync();
    }
    private async Task RunRowControlAsync(InstitutionAccountRow row)
    {
        if (row.Request is { Type: "ACCOUNT_ISSUE", Status: "OPEN" } request)
        {
            if (NoticeDialog.Choose(ModalOwner, "이 요청을 취소하시겠습니까?", "처리 중인 요청만 취소할 수 있습니다.", ["요청 취소", "돌아가기"]) != 0) return;
            try { await Client.CancelRequestAsync(request.Id, Lifetime.Token); await RefreshAsync(); }
            catch (Exception ex) { ErrorText.Text = "요청을 취소하지 못했습니다. " + ex.Message; }
            return;
        }
        if (row.Account is { } account) await OpenAccountAsync(account);
    }
    private async Task OpenAccountAsync(InstitutionAccount account)
    {
        try
        {
            var jobs = await Client.AccountJobsAsync(account.LoginId, ct: Lifetime.Token);
            var detail = new InstitutionAccountDetailDialog(account, jobs,
                locked => Client.SetLockAsync(account.LoginId, locked, Lifetime.Token),
                (alias, email, limit) => Client.UpdateAccountAsync(account.LoginId, alias, email, limit, Lifetime.Token)) { Owner = ModalOwner };
            if (detail.ShowDialog() == true) await RefreshAsync();
        }
        catch (Exception ex) { ErrorText.Text = "계정 정보를 불러오지 못했습니다. " + ex.Message; }
    }
    private async Task OpenNoticeAsync(InstitutionNotice notice)
    {
        var dialog = new InstitutionNoticeDialog(notice, CreditRequest.IsEnabled) { Owner = ModalOwner };
        if (dialog.ShowDialog() == true && dialog.CreditRequested) await RequestCreditAsync();
    }
    private const string ReceiptSaveFailed = "증빙 받는 사람을 바꾸지 못했습니다. ";
    private bool savingReceipt;
    internal bool SavingReceipt => savingReceipt;
    /// <summary>Saves the field when it differs from the server value; a malformed address stays in the field with a hint and is never sent.</summary>
    internal async Task SaveReceiptEmailAsync()
    {
        if (savingReceipt || refreshing || !ReceiptAddress.IsEnabled || Lifetime.IsCancellationRequested) return;
        var email = ReceiptAddress.Text.Trim();
        if (email == (receiptEmail ?? "")) { ReceiptAddress.Text = email; return; }
        if (email.Length > 0 && !InstitutionClient.IsReceiptEmail(email)) { ReceiptHint.Visibility = Visibility.Visible; return; }
        savingReceipt = true; ReceiptAddress.IsReadOnly = true;
        try
        {
            await Client.SetReceiptEmailAsync(email, Lifetime.Token);
            receiptEmail = email.Length == 0 ? null : email; ReceiptAddress.Text = email;
            if (ErrorText.Text.StartsWith(ReceiptSaveFailed, StringComparison.Ordinal)) ErrorText.Text = "";
        }
        catch (OperationCanceledException) when (Lifetime.IsCancellationRequested) { }
        catch (Exception ex) { ReceiptAddress.Text = receiptEmail ?? ""; ErrorText.Text = ReceiptSaveFailed + ex.Message; }
        finally { savingReceipt = false; ReceiptAddress.IsReadOnly = false; }
    }
    private Task OpenOrderAsync(InstitutionOrder order)
    {
        var dialog = new InstitutionOrderDialog(order, receiptEmail, () => OpenQuoteForAsync(order)) { Owner = ModalOwner };
        dialog.ShowDialog(); return Task.CompletedTask;
    }
    internal Action<string> ReceiptLauncher { get; set; } = url => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    private Task OpenReceiptForAsync(InstitutionOrder order) => OpenOrderFileAsync(order.ReceiptFileName, ct => Client.ReceiptAsync(order.Id, ct), "증빙을 열지 못했습니다. ");
    /// <summary>The quote opens like the receipt: a short-lived presigned URL handed to the shell.</summary>
    private Task OpenQuoteForAsync(InstitutionOrder order) => OpenOrderFileAsync(order.QuoteFileName, ct => Client.QuoteAsync(order.Id, ct), "견적서를 열지 못했습니다. ");
    private async Task OpenOrderFileAsync(string? fileName, Func<CancellationToken, Task<InstitutionReceipt>> fetch, string failure)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return;
        var marker = api.SessionMarker;
        var owner = api.Account;
        if (marker is null || marker != institutionSessionMarker || owner?.Role != "ROLE_ORG_ADMIN" || api.SessionMarker != marker) return;
        var lifetime = Lifetime.Token;
        try
        {
            var file = await fetch(lifetime);
            // A late response from a closed window or an earlier login must never launch a URL.
            // A token refresh retains the marker; logout and even a same-account login replace it.
            if (lifetime.IsCancellationRequested || !Root.IsVisible || api.SessionMarker != marker || api.Account is not { Role: "ROLE_ORG_ADMIN" } current || current.LoginId != owner.LoginId) return;
            ReceiptLauncher(file.Url);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex) { if (!lifetime.IsCancellationRequested && Root.IsVisible && api.SessionMarker == marker && api.Account is { Role: "ROLE_ORG_ADMIN" } current && current.LoginId == owner.LoginId) ErrorText.Text = failure + ex.Message; }
    }
}
