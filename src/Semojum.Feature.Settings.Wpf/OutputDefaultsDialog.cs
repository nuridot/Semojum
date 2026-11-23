using System.Windows;
using System.Windows.Controls;
using Semojum.Feature.Settings;

namespace Semojum.Desktop.Dialogs;

public sealed class OutputDefaultsDialog : CompactDialog
{
    public OutputDefaultsDialog(SettingsClient client) : base("계정 출력 기본값", 520, 520)
    {
        var fields = new StackPanel { Margin = new Thickness(24, 16, 24, 12) };
        Body(new ScrollViewer { Content = fields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var paper = new TextBox { MaxLength = 30 };
        var lines = new TextBox(); var cells = new TextBox(); var start = new TextBox();
        var numbering = new ComboBox { ItemsSource = new[] { "홀수 면", "모든 면" } };
        var unit = new ComboBox { ItemsSource = new[] { "음절", "어절" } };
        var duplex = new CheckBox { Content = "양면 출력" };
        SettingRow(fields, "인쇄 용지", paper); SettingRow(fields, "페이지당 줄 수", lines);
        SettingRow(fields, "줄당 칸 수", cells); SettingRow(fields, "점자 시작 면", start);
        SettingRow(fields, "쪽 번호 표시", numbering); SettingRow(fields, "줄바꿈 단위", unit);
        fields.Children.Add(duplex);
        void Display(AccountOutputOptions value)
        {
            paper.Text = value.PaperSize; lines.Text = value.LinesPerPage.ToString(); cells.Text = value.CellsPerLine.ToString();
            start.Text = value.BraillePageStart.ToString(); numbering.SelectedIndex = value.PageNumberLine == "odd" ? 0 : 1;
            unit.SelectedIndex = value.LineBreakUnit == "syllable" ? 0 : 1; duplex.IsChecked = value.Duplex;
        }
        Action("취소", () => DialogResult = false);
        var save = Action("기본값 저장", async () =>
        {
            if (!int.TryParse(lines.Text, out var l) || !int.TryParse(cells.Text, out var c) || !int.TryParse(start.Text, out var s))
                throw new ArgumentException("줄 수, 칸 수, 시작 면은 정수로 입력해 주세요.");
            await client.SaveOutputAsync(new(paper.Text, l, c, s, numbering.SelectedIndex == 0 ? "odd" : "every",
                duplex.IsChecked == true, unit.SelectedIndex == 0 ? "syllable" : "word"), Lifetime.Token);
            DialogResult = true;
        }, true);
        save.IsEnabled = false;
        async Task Reload()
        {
            Display(await client.GetOutputAsync(Lifetime.Token));
            save.IsEnabled = true;
        }
        Action("다시 불러오기", Reload);
        OnLoad(Reload);
    }
}
