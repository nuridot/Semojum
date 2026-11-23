using System.Windows;
using System.Windows.Controls;
using Semojum.Core;
using Semojum.Feature.Settings;

namespace Semojum.Desktop.Dialogs;

public sealed class PageReconversionDialog : ConversionFormDialog
{
    public string? CreatedJob { get; private set; }
    public PageReconversionDialog(int page, IReadOnlySet<int> available, ProofPricing pricing,
        Func<int[], string, Guid, Task<string>> start, Func<int[],bool> manual, string? initialModel = null) : base("쪽 다시 변환", 600, 602)
    {
        var rows = new StackPanel();
        Fields.Children.Add(Card(rows, new(16, 12, 16, 12)));
        var scope = new StackPanel();
        FormRow(rows, "다시 변환할 쪽", scope, 162);
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
        var proof = new ProofModelSelector(pricing, initialModel);
        Section("교열 모델").Children.Add(proof);
        int[] Pages() => ReconversionPages.Parse(single.IsChecked == true ? page.ToString() : range.Text, available);
        var requestId = Guid.NewGuid();
        void Changed()
        {
            requestId = Guid.NewGuid();
            try { proof.Configure(Pages().Length); }
            catch (ArgumentException ex) { proof.Configure(null, ex.Message); }
        }
        single.Checked += (_, _) => Changed(); chosen.Checked += (_, _) => Changed();
        range.TextChanged += (_, _) => Changed(); proof.ModelChanged += Changed;
        Changed(); CancelButton();
        FooterButton("다시 변환", async () =>
        {
            var selected = Pages();
            var editedPages = selected.Where(n => manual([n])).ToArray();
            if (editedPages.Length > 0 && NoticeDialog.Choose(this, "고친 내용이 있는 쪽을 다시 변환하시겠습니까?",
                 $"{string.Join(" · ", editedPages)}쪽에 직접 고친 내용이 있습니다. 새 탭에서는 해당 쪽을 새 결과로 바꿉니다. 원래 탭은 보관합니다.", ["덮어쓰기", "취소"], width: 520, height: 178) != 0) return;
            if (proof.Model is not { } model) throw new ArgumentException("교열 모델을 선택해 주세요.");
            CreatedJob = await start(selected, model.Code, requestId);
            DialogResult = true;
        }, true);
    }
}
