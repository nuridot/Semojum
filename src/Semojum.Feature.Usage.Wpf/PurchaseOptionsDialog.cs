using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Semojum.Core.Integration;

namespace Semojum.Desktop.Dialogs;

public sealed class PurchaseOptionsDialog : MyPageActionDialog
{
    public enum Section { Plan, TopUp }
    private const string Billing = "/api/members/me/billing";
    private readonly SemojumApiClient api;
    private readonly Guid? marker;
    private readonly Grid cards = new();
    private readonly StackPanel summary = new();
    private readonly TextBox custom = new() { MaxLength = 18 };
    private readonly Button save;
    private readonly Button retry;
    private readonly Border customRow;
    private readonly Dictionary<long, Price> presets = [];
    private readonly Dictionary<string, TextBlock> planPrices = [];
    private Choice? selection;
    private Price? price;
    private Guid requestId;
    private int revision;
    private sealed record Choice(string Kind, string? PlanCode = null, long? Credits = null);
    private sealed record Price(long Credits, long Amount, int? DiscountPercent, long? Discount, long? Vat);

    public PurchaseOptionsDialog(SemojumApiClient api, Section section) : base(section == Section.Plan ? "요금제 업그레이드" : "추가 크레딧 구매",
        section == Section.Plan ? 760 : 640, section == Section.Plan ? 426 : 522)
    {
        this.api = api; marker = api.SessionMarker;
        Fields.Children.Add(cards);
        var customFields = new StackPanel();
        FormRow(customFields, "구매할 크레딧", Input(custom, "구매할 크레딧", "수량 입력"), 120);
        customRow = Card(customFields, new(16, 12, 16, 12)); customRow.Margin = new(0, 12, 0, 0);
        customRow.Visibility = Visibility.Collapsed; Fields.Children.Add(customRow);
        if (section == Section.TopUp)
        {
            var totals = Card(summary, new(16, 6, 16, 6)); totals.Margin = new(0, 16, 0, 0); Fields.Children.Add(totals); ShowTotals(null);
        }
        Fields.Children.Add(new TextBlock { Text = "결제 수단", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = EditorModalDesign.Color("#5F5F5F"), Margin = new(0, 16, 0, 8) });
        var methods = Card(new TextBlock { Text = "결제 수단 연동 준비 중입니다. 주문안을 저장하면 결제 내역에서 확인할 수 있습니다.",
            FontSize = 14, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center }, new(16, 12, 16, 12));
        methods.MinHeight = section == Section.Plan ? 108 : 76; Fields.Children.Add(methods);
        retry = Button("견적 다시 확인", () => SelectAsync(selection)); retry.Visibility = Visibility.Collapsed;
        retry.HorizontalAlignment = HorizontalAlignment.Left; Fields.Children.Add(retry);
        CancelButton(); save = FooterButton("주문안 저장", SaveAsync, true); save.IsEnabled = false;
        custom.TextChanged += async (_, _) =>
        {
            if (customRow.Visibility != Visibility.Visible) return;
            var valid = long.TryParse(custom.Text, NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var count) && count > 0;
            await SelectAsync(valid ? new("TOPUP", Credits: count) : null, debounce: true);
        };
        OnLoad(section == Section.Plan ? LoadPlansAsync : LoadTopUpsAsync);
    }

    private Task<JsonElement> Send(HttpMethod method, string path, object? body = null) =>
        api.SendAuthenticatedAsync(method, path, body, Lifetime.Token, marker);

    private async Task<Price> QuoteAsync(Choice choice)
    {
        var data = await Send(HttpMethod.Post, Billing + "/quote", choice);
        long? Amount(string name) => data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) ? number : null;
        var credits = data.GetProperty("credits").GetInt64(); var total = data.GetProperty("amountKrw").GetInt64();
        if (credits <= 0 || total <= 0 || choice.Credits.HasValue && credits != choice.Credits)
            throw new InvalidDataException("서버 견적을 확인해 주세요.");
        return new(credits, total, (int?)Amount("discountPercent"), Amount("discountKrw"), Amount("vatKrw"));
    }

    private async Task LoadPlansAsync()
    {
        var wallet = await Send(HttpMethod.Get, "/api/users/credit/wallet");
        var active = wallet.TryGetProperty("subscription", out var subscription) && subscription.ValueKind == JsonValueKind.Object;
        string? Current(string key) => active && subscription.TryGetProperty(key, out var value) ? value.GetString() : null;
        var products = await Send(HttpMethod.Get, Billing + "/plans");
        RadioButton? firstPurchasable = null, nextPlan = null;
        var passedCurrent = false;
        foreach (var plan in products.EnumerateArray())
        {
            var code = plan.GetProperty("code").GetString()!; var name = plan.GetProperty("name").GetString()!;
            var amount = plan.GetProperty("amountKrw").GetInt64(); var count = plan.GetProperty("credits").GetInt64();
            bool current = Current("planCode") is { } activeCode ? activeCode == code : Current("planName") == name;
            var (button, content) = AddCard(name, 144, current ? "지금" : null);
            var priceLabel = new TextBlock { Text = $"{amount:N0}원 / 월", FontSize = 19, FontWeight = FontWeights.SemiBold, Margin = new(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };
            content.Children.Add(priceLabel); planPrices[code] = priceLabel;
            content.Children.Add(new TextBlock { Text = $"크레딧 {(amount == 0 ? "" : "월 ")}{count:N0}", FontSize = 12, Margin = new(0, 8, 0, 0) });
            var pick = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 8, 0, 0) };
            var dot = new Border { Width = 6, Height = 6, Background = Brushes.White, CornerRadius = new(3), Visibility = Visibility.Hidden,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var ring = new Border { Width = 16, Height = 16, CornerRadius = new(8), BorderThickness = new(1), BorderBrush = EditorModalDesign.Color("#5F5F5F"), Child = dot };
            pick.Children.Add(ring); pick.Children.Add(new TextBlock { Text = "고르기", FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new(8, 0, 0, 0) });
            content.Children.Add(pick);
            var choice = new Choice("PLAN", PlanCode: code);
            var eligible = amount > 0 && count > 0 && !current;
            button.IsEnabled = eligible;
            button.Checked += async (_, _) =>
            {
                ring.Background = ring.BorderBrush = EditorModalDesign.Color("#005FB8"); dot.Visibility = Visibility.Visible;
                await SelectAsync(choice);
            };
            button.Unchecked += (_, _) => { ring.Background = Brushes.Transparent; ring.BorderBrush = EditorModalDesign.Color("#5F5F5F"); dot.Visibility = Visibility.Hidden; };
            if (eligible)
            {
                firstPurchasable ??= button;
                if (passedCurrent) nextPlan ??= button;
            }
            passedCurrent |= current;
        }
        if ((nextPlan ?? firstPurchasable) is { } initial) initial.IsChecked = true;
        else Status.Text = "변경할 수 있는 요금제가 없습니다.";
    }

    private async Task LoadTopUpsAsync()
    {
        RadioButton? first = null;
        foreach (var n in new long[] { 100000, 300000, 1000000 })
        {
            var (button, content) = AddCard(n.ToString("N0"), 84);
            first ??= button;
            button.Checked += async (_, _) => { customRow.Visibility = Visibility.Collapsed; await SelectAsync(new("TOPUP", Credits: n)); };
            try
            {
                var quote = await QuoteAsync(new("TOPUP", Credits: n));
                presets[n] = quote;
                if (quote.DiscountPercent is > 0)
                {
                    var badge = new Border { Background = EditorModalDesign.Color("#DFF6DD"), Padding = new(8, 1, 8, 1), Margin = new(0, 8, 0, 0),
                        CornerRadius = new(4), HorizontalAlignment = HorizontalAlignment.Left, Child = new TextBlock { Text = $"{quote.DiscountPercent}% 할인", FontSize = 12, Foreground = EditorModalDesign.Color("#107C10") } };
                    content.Children.Add(badge);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { /* A selection can retry a missing preset quote individually. */ }
        }
        var (other, _) = AddCard("직접 입력", 54);
        other.Checked += async (_, _) =>
        {
            customRow.Visibility = Visibility.Visible; custom.Focus();
            await SelectAsync(long.TryParse(custom.Text, NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var n) && n > 0 ? new("TOPUP", Credits: n) : null);
        };
        if (first is not null) first.IsChecked = true;
    }

    private (RadioButton Button, StackPanel Content) AddCard(string name, double height, string? badge = null)
    {
        if (cards.ColumnDefinitions.Count > 0) cards.ColumnDefinitions.Add(new() { Width = new(12) });
        cards.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        var content = new StackPanel { Margin = new(15), VerticalAlignment = VerticalAlignment.Center };
        var title = new DockPanel();
        if (badge is not null)
        {
            var tag = new Border { Background = EditorModalDesign.Color("#F0F0F0"), CornerRadius = new(4), Padding = new(6, 0, 6, 0),
                Child = new TextBlock { Text = badge, FontSize = 12 } };
            DockPanel.SetDock(tag, Dock.Right); title.Children.Add(tag);
        }
        var heading = new TextBlock { Text = name, FontSize = 14, FontWeight = FontWeights.SemiBold };
        title.Children.Add(heading); content.Children.Add(title);
        var button = new RadioButton { Content = content, GroupName = "PurchaseSelection", Height = height - 2, MinWidth = 0,
            Background = Brushes.White, BorderBrush = EditorModalDesign.Color("#EAEAEA"), BorderThickness = new(1), VerticalAlignment = VerticalAlignment.Top };
        AutomationProperties.SetName(button, name);
        var surface = new FrameworkElementFactory(typeof(Border), "Card"); surface.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        surface.SetValue(Border.BackgroundProperty, Brushes.White); surface.SetValue(Border.BorderBrushProperty, button.BorderBrush); surface.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        surface.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
        var template = new ControlTemplate(typeof(RadioButton)) { VisualTree = surface };
        var selected = new Trigger { Property = RadioButton.IsCheckedProperty, Value = true };
        selected.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(2), "Card"));
        selected.Setters.Add(new Setter(Border.BorderBrushProperty, EditorModalDesign.Color("#005FB8"), "Card")); template.Triggers.Add(selected);
        button.Checked += (_, _) => { button.Height = height; heading.Foreground = EditorModalDesign.Color("#003E92"); };
        button.Unchecked += (_, _) => { button.Height = height - 2; heading.ClearValue(TextBlock.ForegroundProperty); };
        button.Template = template; Grid.SetColumn(button, cards.ColumnDefinitions.Count - 1); cards.Children.Add(button);
        return (button, content);
    }

    private async Task SelectAsync(Choice? choice, bool debounce = false)
    {
        var version = ++revision; selection = choice; price = null; save.IsEnabled = false;
        Status.Text = ""; retry.Visibility = Visibility.Collapsed; ShowTotals(null);
        if (choice is null) return;
        try
        {
            if (debounce) await Task.Delay(250, Lifetime.Token);
            if (version != revision) return;
            var quote = choice.Credits is { } count && presets.TryGetValue(count, out var preset) ? preset : await QuoteAsync(choice);
            if (version != revision || Lifetime.IsCancellationRequested) return;
            price = quote; requestId = Guid.NewGuid(); save.IsEnabled = true; ShowTotals(quote);
            if (choice.PlanCode is { } code && planPrices.TryGetValue(code, out var label))
                label.Text = $"{quote.Amount:N0}원 / 월";
        }
        catch (OperationCanceledException) when (Lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (version != revision || Lifetime.IsCancellationRequested) return;
            Status.Text = ex.Message; retry.Visibility = Visibility.Visible;
        }
    }

    private void ShowTotals(Price? value)
    {
        summary.Children.Clear();
        void Row(string label, string amount, bool bold = false)
        {
            var row = new DockPanel { Height = 40, LastChildFill = false };
            row.Children.Add(new TextBlock { Text = label, FontSize = 14, Foreground = EditorModalDesign.Color("#5F5F5F"), VerticalAlignment = VerticalAlignment.Center });
            var number = new TextBlock { Text = amount, FontSize = 14, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(number, Dock.Right); row.Children.Add(number); summary.Children.Add(row);
        }
        Row("크레딧", value?.Credits.ToString("N0") ?? "—");
        Row("할인", value is { DiscountPercent: { } percent, Discount: { } discount } ? $"{percent}% (−{discount:N0}원)" : "—");
        Row("부가세", value?.Vat is { } vat ? $"{vat:N0}원" : "—");
        Row("결제 금액", value is null ? "—" : $"{value.Amount:N0}원", true);
    }

    private async Task SaveAsync()
    {
        if (selection is null || price is null) return;
        try { await Send(HttpMethod.Post, Billing + "/orders", new { requestId, selection, expectedAmountKrw = price.Amount }); }
        catch (SemojumApiException ex) when (ex.Code == "MEMBER4091")
        {
            if (selection.Credits is { } count) presets.Remove(count);
            await SelectAsync(selection);
            if (price is not null) Status.Text = "견적이 변경되었습니다. 새 금액을 확인한 뒤 주문안을 저장해 주세요.";
            return;
        }
        DialogResult = true;
    }
}
