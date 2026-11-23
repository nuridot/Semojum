using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Semojum.Desktop.Dialogs;

internal sealed class InstitutionBillingDialog : CompactDialog
{
    public InstitutionBillingDialog(InstitutionClient client, Func<bool> currentAccount, Action<string> launch)
        : base("계약·청구 문서", 720, 440)
    {
        var tabs = new TabControl { Margin = new(12) };
        DataGrid Table(string tab, params (string Label, string Property)[] columns)
        {
            var table = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true,
                CanUserAddRows = false, SelectionMode = DataGridSelectionMode.Single };
            foreach (var (label, property) in columns)
                table.Columns.Add(new DataGridTextColumn { Header = label, Binding = new Binding(property), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            tabs.Items.Add(new TabItem { Header = tab, Content = table });
            return table;
        }
        var contracts = Table("계약", ("계약 번호", "Number"), ("상태", "Status"), ("시작일", "Start"), ("종료일", "End"));
        var invoices = Table("청구", ("청구 번호", "Number"), ("상태", "Status"), ("합계(원)", "Total"), ("미납액(원)", "Outstanding"));
        var documents = Table("공유 문서", ("문서 번호", "Number"), ("종류", "Type"), ("연결된 청구", "Invoice"));
        var download = Button("선택한 문서 내려받기", () => RunAsync(async () =>
        {
            if (!currentAccount() || documents.SelectedItem is not InstitutionSharedDocument document) return;
            var file = await client.DocumentFileAsync(document.Id, Lifetime.Token);
            if (IsVisible && !Lifetime.IsCancellationRequested && currentAccount()) launch(file.Url);
        }));
        void Selection() => download.IsEnabled = tabs.SelectedIndex == 2 && documents.SelectedItem is InstitutionSharedDocument;
        tabs.SelectionChanged += (_, _) => Selection();
        Selection();
        Actions.Children.Add(download);
        Actions.Children.Add(Button("닫기", () => { Close(); return Task.CompletedTask; }));
        Body(tabs);
        OnLoad(async () =>
        {
            var contractRows = client.ContractsAsync(Lifetime.Token);
            var invoiceRows = client.InvoicesAsync(Lifetime.Token);
            var documentRows = client.DocumentsAsync(Lifetime.Token);
            await Task.WhenAll(contractRows, invoiceRows, documentRows);
            if (!currentAccount() || Lifetime.IsCancellationRequested) return;
            contracts.ItemsSource = await contractRows;
            invoices.ItemsSource = await invoiceRows;
            documents.ItemsSource = await documentRows;
        });
    }
}
