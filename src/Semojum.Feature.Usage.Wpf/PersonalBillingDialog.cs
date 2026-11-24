using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Semojum.Core.Integration;

namespace Semojum.Desktop.Dialogs;

public sealed class PersonalBillingDialog : CompactDialog
{
    private const string Billing = "/api/members/me/billing";
    private readonly SemojumApiClient api;
    private readonly Guid? accountSession;

    private readonly TextBlock wallet = new() { FontSize = 18, Margin = new(0, 0, 0, 12) };

    private readonly ListBox orders = new() { MinHeight = 100, DisplayMemberPath = nameof(Order.Label) };
    private readonly ListBox movements = new() { MinHeight = 100 };
    private readonly ListBox invoices = new() { MinHeight = 100, DisplayMemberPath = nameof(Order.Label) };
    private readonly Button more;
    private long? before;

    internal readonly record struct OrderDates(DateTimeOffset HistoryDate, DateTimeOffset? PaidDate)
    {
        public string? InvoiceDate => PaidDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static OrderDates Read(JsonElement order)
        {
            var korea = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
            DateTimeOffset? paid = order.TryGetProperty("paidAt", out var value) && value.ValueKind == JsonValueKind.String
                && value.TryGetDateTimeOffset(out var instant) ? TimeZoneInfo.ConvertTime(instant, korea) : null;
            return new(paid ?? TimeZoneInfo.ConvertTime(order.GetProperty("createdAt").GetDateTimeOffset(), korea), paid);
        }
    }

    private sealed record Order(Guid Id, OrderDates Dates, string Kind, long Credits, long Total, string State, bool HasInvoice)
    {
        public string Label => $"{Dates.HistoryDate:yyyy-MM-dd HH:mm}   {(Kind == "PLAN" ? "요금제" : "추가 구매")}   {Credits:N0} 크레딧   {Total:N0}원   {State switch {
            "DRAFT" => "주문안", "CANCELLED" => "취소", "PAID" => "결제 완료", "REFUNDED" => "환불", _ => State }}";
    }

    public enum Section { Overview, Orders, Invoices }

    public PersonalBillingDialog(SemojumApiClient api, Section section = Section.Overview) : base("개인 크레딧과 결제", 720, 540)
    {
        this.api = api;
        accountSession = api.SessionMarker;
        var tabs = new TabControl { Margin = new(16) };
        Body(tabs);
        StackPanel Tab(string label)
        {
            var panel = new StackPanel { Margin = new(12) };
            tabs.Items.Add(new TabItem { Header = label, Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
            return panel;
        }
        Button Do(string label, Func<Task> action) => Button(label, () => RunAsync(action));
        var buy = Tab("내 크레딧");
        buy.Children.Add(wallet);
        Task Purchase(PurchaseOptionsDialog.Section kind)
        {
            var saved = new PurchaseOptionsDialog(api, kind) { Owner = this }.ShowDialog();
            return saved == true ? ReloadAsync() : Task.CompletedTask;
        }
        buy.Children.Add(Do("추가 크레딧 구매", () => Purchase(PurchaseOptionsDialog.Section.TopUp)));
        buy.Children.Add(Do("요금제 업그레이드", () => Purchase(PurchaseOptionsDialog.Section.Plan)));

        var ledger = Tab("최근 30일 상세 내역");
        ledger.Children.Add(movements); more = Do("더 보기", () => LoadMovementsAsync(false)); ledger.Children.Add(more);
        var history = Tab("12개월 결제 내역");
        history.Children.Add(orders);
        history.Children.Add(Do("주문안 취소", async () =>
        {
            if (orders.SelectedItem is not Order { State: "DRAFT" } item) throw new ArgumentException("취소할 주문안을 선택해 주세요.");
            if (NoticeDialog.Choose(this, "주문안을 취소할까요?", item.Label, ["주문안 취소", "돌아가기"]) != 0) return;
            await Send(HttpMethod.Post, Billing + $"/orders/{item.Id:D}/cancel");
            await LoadHistoryAsync();
        }));
        var documents = Tab("청구서");
        documents.Children.Add(invoices); documents.Children.Add(Do("PDF 내려받기", DownloadInvoiceAsync));
        Action("새로 고침", ReloadAsync); Action("닫기", Close);
        tabs.SelectedIndex = section switch { Section.Orders => 2, Section.Invoices => 3, _ => 0 };
        OnLoad(ReloadAsync);
    }

    private Task<JsonElement> Send(HttpMethod method, string path, object? body = null) =>
        api.SendAuthenticatedAsync(method, path, body, Lifetime.Token, accountSession);


    private async Task ReloadAsync()
    {
        var data = await Send(HttpMethod.Get, "/api/users/credit/wallet");
        var sub = data.GetProperty("subscription");
        var plan = sub.ValueKind == JsonValueKind.Object ? sub.GetProperty("planName").GetString() : "구독 없음";
        wallet.Text = $"사용 가능 {data.GetProperty("available").GetInt64():N0} 크레딧 · {plan}";

        await LoadHistoryAsync();
        await LoadMovementsAsync(true);
    }
    private async Task LoadHistoryAsync()
    {
        var data = await Send(HttpMethod.Get, Billing + "/orders");
        var rows = data.EnumerateArray().Select(o => new Order(o.GetProperty("id").GetGuid(), OrderDates.Read(o),
            o.GetProperty("kind").GetString()!, o.GetProperty("credits").GetInt64(), o.GetProperty("amountKrw").GetInt64(),
            o.GetProperty("status").GetString()!, o.GetProperty("hasInvoice").GetBoolean())).ToArray();
        orders.ItemsSource = rows; invoices.ItemsSource = rows.Where(o => o.HasInvoice).ToArray();
    }
    private async Task LoadMovementsAsync(bool first)
    {
        var rows = (await Send(HttpMethod.Get, Billing + "/transactions" + (first || before is null ? "" : "?before=" + before.Value.ToString(CultureInfo.InvariantCulture)))).EnumerateArray().ToArray();
        if (first) { movements.Items.Clear(); before = null; }
        foreach (var r in rows)
        {
            var kind = r.GetProperty("entryType").GetString() switch { "GRANT" => "지급", "CAPTURE" => "사용", "RESERVE" => "예약", "RELEASE" => "예약 해제", "EXPIRE" => "만료", "ADJUST" => "조정", var other => other };
            var balance = r.GetProperty("balanceAfter") is { ValueKind: JsonValueKind.Number } amount ? amount.GetInt64().ToString("N0") : "—";
            movements.Items.Add($"{r.GetProperty("createdAt").GetDateTimeOffset().ToLocalTime():MM-dd HH:mm}   {kind}   {r.GetProperty("balanceDelta").GetInt64():+#,0;-#,0;0}   예약 변동 {r.GetProperty("reservedDelta").GetInt64():+#,0;-#,0;0}   잔액 {balance}");
            before = r.GetProperty("id").GetInt64();
        }
        if (first && rows.Length == 0) movements.Items.Add("최근 30일 거래가 없습니다.");
        more.IsEnabled = rows.Length == 100;
    }
    private async Task DownloadInvoiceAsync()
    {
        if (invoices.SelectedItem is not Order { HasInvoice: true } item) throw new ArgumentException("내려받을 청구서를 선택해 주세요.");
        await PersonalInvoiceDialog.SavePdfAsync(this, api, accountSession, item.Id, item.Dates.InvoiceDate, Lifetime.Token);
    }
}
