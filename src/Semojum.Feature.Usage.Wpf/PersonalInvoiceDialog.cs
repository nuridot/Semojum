using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Semojum.Core.Integration;

namespace Semojum.Desktop.Dialogs;

internal sealed record PersonalInvoice(Guid OrderId, string? PaidDate, string Item, string Total);

internal sealed class PersonalInvoiceDialog : MyPageActionDialog
{
    public PersonalInvoiceDialog(SemojumApiClient api, PersonalInvoice invoice) : base(invoice.PaidDate is null ? "청구서" : $"청구서 · {invoice.PaidDate}", 560, 490)
    {
        var identity = api.SessionMarker;
        var detail = new StackPanel(); var amounts = new StackPanel();
        // The service retains the issued PDF. Its number, addressee, tax split and method
        // are not part of the order record; presenting estimates as invoice details would be incorrect.
        InvoiceRow(detail, "청구 번호", null); InvoiceRow(detail, "받는 곳", null);
        InvoiceRow(detail, "결제일", invoice.PaidDate); InvoiceRow(detail, "품목", invoice.Item);
        Fields.Children.Add(Card(detail, new(16, 6, 16, 6)));
        InvoiceRow(amounts, "공급가액", null); InvoiceRow(amounts, "부가세", null);
        InvoiceRow(amounts, "합계", invoice.Total, true); InvoiceRow(amounts, "결제 수단", null);
        var card = Card(amounts, new(16, 6, 16, 6)); card.Margin = new(0, 16, 0, 0); Fields.Children.Add(card);
        CancelButton("닫기");
        FooterButton("PDF 내려받기", () => SavePdfAsync(this, api, identity, invoice.OrderId, invoice.PaidDate, Lifetime.Token), true).Width = 99;
    }
    private static void InvoiceRow(Panel panel, string label, string? value, bool bold = false)
    {
        var row = new DockPanel { Height = 40, LastChildFill = false };
        row.Children.Add(new TextBlock { Text = label, FontSize = 14, Foreground = EditorModalDesign.Color("#5F5F5F"), VerticalAlignment = VerticalAlignment.Center });
        var field = new TextBlock { Text = value ?? "—", FontSize = 14, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            ToolTip = value is null ? "발급된 청구서 PDF에서 확인할 수 있습니다." : null, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(field, Dock.Right); row.Children.Add(field); panel.Children.Add(row);
    }

    internal static async Task SavePdfAsync(Window owner, SemojumApiClient api, Guid? identity, Guid orderId, string? date, CancellationToken token)
    {
        var picker = new SaveFileDialog { FileName = date is null ? "누리닷 청구서" : $"누리닷 청구서 {date}", Filter = "청구서 PDF|*.pdf", DefaultExt = ".pdf" };
        if (picker.ShowDialog(owner) != true) return;
        if (identity is null || api.SessionMarker != identity) throw new InvalidOperationException("계정이 변경되었습니다. 결제 내역을 다시 열어 주세요.");
        var bytes = await api.DownloadPersonalInvoiceAsync(orderId, token);
        var temp = picker.FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await File.WriteAllBytesAsync(temp, bytes, token); File.Move(temp, picker.FileName, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
