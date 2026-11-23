using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Semojum.Desktop.Dialogs;

internal sealed class BraillePrintDialog : ConversionFormDialog
{
    internal BraillePrintDialog(int total, Func<Task<(string[] Queues, string? Default)>> loadQueues,
        Func<string, int, int, int, Task> print) : base("점자 인쇄", 480, 274)
    {
        if (total < 1) throw new ArgumentOutOfRangeException(nameof(total));
        AlignLabelBaseline();
        var rows = new StackPanel(); Fields.Children.Add(Card(rows, new(16, 12, 16, 12)));
        var printers = new ComboBox();
        FormRow(rows, "프린터", SelectField(printers, "프린터"), 162);
        var first = new TextBox { Text = "1" };
        var last = new TextBox { Text = total.ToString(CultureInfo.CurrentCulture) };
        var copies = new TextBox { Text = "1" };
        var range = new StackPanel { Orientation = Orientation.Horizontal };
        range.Children.Add(NumberField(first, "시작 면", 1, total));
        range.Children.Add(new TextBlock { Text = "~", Width = 33, TextAlignment = TextAlignment.Center, Foreground = EditorModalDesign.Color("#5F5F5F") });
        range.Children.Add(NumberField(last, "마지막 면", 1, total)); FormRow(rows, "면", range, 162);
        var count = new StackPanel { Orientation = Orientation.Horizontal };
        count.Children.Add(NumberField(copies, "인쇄 매수", 1, 100));
        count.Children.Add(new TextBlock { Text = "부 (1~100)", FontSize = 12, Margin = new(8,0,0,0), Foreground = EditorModalDesign.Color("#5F5F5F") });
        FormRow(rows, "매수", count, 162);
        CancelButton();
        var submit = FooterButton("인쇄", async () =>
        {
            if (printers.SelectedItem is not string queue) throw new ArgumentException("프린터를 선택해 주세요.");
            if (!int.TryParse(first.Text, out var start) || !int.TryParse(last.Text, out var end) || start < 1 || end < start || end > total)
                throw new ArgumentException($"1–{total}면 안에서 시작 면과 마지막 면을 확인해 주세요.");
            if (!int.TryParse(copies.Text, out var n) || n is < 1 or > 100) throw new ArgumentException("인쇄 부수는 1–100부로 입력해 주세요.");
            await print(queue, start, end, n); DialogResult = true;
        }, true);
        submit.IsEnabled = false;
        Loaded += async (_, _) =>
        {
            try
            {
                var (queues, preferred) = await loadQueues();
                if (Lifetime.IsCancellationRequested) return;
                printers.ItemsSource = queues;
                printers.SelectedItem = queues.FirstOrDefault(q => string.Equals(q, preferred, StringComparison.OrdinalIgnoreCase)) ?? queues.FirstOrDefault();
                submit.IsEnabled = printers.SelectedItem is string;
                if (!submit.IsEnabled) Status.Text = "사용할 수 있는 프린터가 없습니다. Windows에서 점자 프린터를 추가해 주세요.";
            }
            catch (Exception ex) { if (!Lifetime.IsCancellationRequested) Status.Text = "프린터 목록을 읽을 수 없습니다. " + ex.Message; }
        };
    }
}
