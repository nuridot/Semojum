using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Semojum.Desktop.Dialogs;

internal sealed class InstitutionNoticeDialog : InstitutionFormDialog
{
    internal bool CreditRequested { get; private set; }

    internal InstitutionNoticeDialog(InstitutionNotice notice, bool canRequest)
        : base(notice.Title, 560, 206, "institution-notice-detail-close.svg")
    {
        var metadata = new StackPanel { Orientation = Orientation.Horizontal };
        var category = notice.Scope switch { "ORG" => "우리 기관", "ALL" => "전체", _ => notice.Scope };
        var tone = Ink(notice.Scope == "ORG" ? "#9D5D00" : "#5F5F5F");
        metadata.Children.Add(new Border { BorderBrush = tone, BorderThickness = new(1), CornerRadius = new(10), Height = 20,
            Padding = new(8, 0, 8, 0), Child = new TextBlock { Text = category, FontSize = 12, Foreground = tone } });
        var date = InstitutionUsage.InKorea(notice.CreatedAt)?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? notice.CreatedAt;
        metadata.Children.Add(new TextBlock { Text = date, Foreground = Ink("#8A8A8A"), Margin = new(8, 0, 0, 0) }); Fields.Children.Add(metadata);
        var body = Value(notice.Body); body.Margin = new(0, 16, 0, 0); Fields.Children.Add(body);
        var extra = new MenuItem { Header = "크레딧 추가 요청", IsEnabled = canRequest };
        extra.Click += (_, _) => { CreditRequested = true; DialogResult = true; };
        ContextMenu = new ContextMenu(); ContextMenu.Items.Add(extra);
        Dismiss("확인", true);
    }
}

internal sealed class InstitutionOrderDialog : InstitutionFormDialog
{
    internal Button QuoteButton { get; }

    private static string OrderTitle(string date) => DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
        ? "주문 상세 · " + day.ToString("MM-dd", CultureInfo.InvariantCulture) : "주문 상세";

    internal InstitutionOrderDialog(InstitutionOrder order, string? receiptEmail, Func<Task> openQuote)
        : base(OrderTitle(order.OrderDate), 560, 382, "institution-order-detail-close.svg")
    {
        var card = Card();
        var date = Value(order.OrderDate);
        if (order.RequestedBy is { Length: > 0 } requester)
        {
            var when = InstitutionUsage.InKorea(order.RequestedAt)?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            date.ToolTip = string.Join(" · ", new[] { requester, order.RequestedByRole == "ROLE_ORG_ADMIN" ? "관리자" : null, when }.Where(t => !string.IsNullOrEmpty(t)));
        }
        Field(card, "일자", date, true);
        Field(card, "내용", Value(order.Description), true);
        Field(card, "금액", Value($"{order.AmountKrw:N0}원 (부가세 포함)"), true);
        var paid = !string.IsNullOrWhiteSpace(order.PaidAt);
        var status = Value(paid ? "완납" : "미납"); status.Foreground = Ink(paid ? "#107C10" : "#C42B1C");
        Field(card, "결제", status, true);
        Field(card, "계산서", Value(InstitutionStatusTextConverter.Label(order.InvoiceStatus, "invoice")), true);
        Field(card, "증빙 받는 메일", Value(string.IsNullOrWhiteSpace(receiptEmail) ? "—" : receiptEmail), true);
        QuoteButton = FooterAction("견적서 내려받기", () => RunAsync(openQuote), left: true, width: 110);
        QuoteButton.ToolTip = order.QuoteFileName;
        QuoteButton.Visibility = string.IsNullOrWhiteSpace(order.QuoteFileName) ? Visibility.Collapsed : Visibility.Visible;
        Dismiss("닫기", true);
    }
}
