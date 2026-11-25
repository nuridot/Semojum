using System.Windows;
using System.Windows.Controls;
using Semojum.Core;

namespace Semojum.Desktop.Dialogs;

public sealed class PageReconversionDialog : ConversionFormDialog
{
    public PageReconversion? Selection { get; private set; }
    public PageReconversionDialog(int page, IReadOnlySet<int> available,
        Func<int, bool> hasEdits, string? initialModel = null) : base("쪽 재변환", 600, 486)
    {
        var rows = new StackPanel();
        Fields.Children.Add(Card(rows, new(16, 12, 16, 12)));
        var scope = new StackPanel();
        FormRow(rows, "재변환할 쪽", scope, 162);
        var scopeRow = (DockPanel)rows.Children[0];
        ((TextBlock)scopeRow.Children[0]).VerticalAlignment = VerticalAlignment.Top;
        var group = Guid.NewGuid().ToString();
        var single = new RadioButton { Content = $"현재 쪽 ({page}쪽)", GroupName = group, Height = 20, IsChecked = available.Contains(page), IsEnabled = available.Contains(page) };
        var chosen = new RadioButton { Content = "쪽 지정", GroupName = group, IsChecked = !available.Contains(page), VerticalAlignment = VerticalAlignment.Center };
        var range = new TextBox { Width = 200, ToolTip = "예: 3, 5–7" };
        scope.Children.Add(single);
        var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 8, 0, 0) };
        scope.Children.Add(line); chosen.Margin = new(0, 0, 8, 0); line.Children.Add(chosen);
        line.Children.Add(Entry(range, "지정할 쪽", "3, 5-7"));
        range.GotKeyboardFocus += (_, _) => chosen.IsChecked = true;
        var proof = new ProofModelSelector(initialModel);
        FormRow(rows, "지금 설정", new TextBlock { Text = proof.Label, Height = 20, FontSize = 14, Foreground = EditorModalDesign.Color("#5F5F5F"), VerticalAlignment = VerticalAlignment.Center }, 162);
        Section("교열 모델").Children.Add(proof);
        CancelButton();
        FooterButton("재변환", () =>
        {
            var selected = ReconversionPages.Parse(single.IsChecked == true ? page.ToString() : range.Text, available);
            var editedPages = selected.Where(hasEdits).ToArray();
            if (editedPages.Length > 0 && NoticeDialog.Choose(this, "고친 내용이 있는 쪽을 재변환하시겠습니까?",
                 $"{string.Join(" · ", editedPages)}쪽에 직접 고친 내용이 있습니다. 재변환에 성공하면 이 작업의 해당 쪽을 새 결과로 바꿉니다.", ["덮어쓰기", "취소"], width: 520, height: 178) != 0) return Task.CompletedTask;
            Selection = new(selected, proof.Code);
            DialogResult = true;
            return Task.CompletedTask;
        }, true);
    }
}
