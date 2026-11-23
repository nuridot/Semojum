using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Semojum.Feature.Settings;

namespace Semojum.Desktop.Dialogs;

internal sealed class ProofModelSelector : StackPanel
{
    private readonly ProofPricing? pricing;
    private readonly RadioButton textPage;
    private readonly TextBlock amount = new() { FontWeight = FontWeights.SemiBold, FontSize = 14 };
    private readonly StackPanel choices = new();
    private int? pages;
    private string? message;
    public ProofPrice? Model { get; private set; }
    public event Action? ModelChanged;

    public ProofModelSelector(ProofPricing? pricing, string? initialModel = null)
    {
        this.pricing = pricing;
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/Semojum.Feature.Documents.Wpf;component/ProofChoice.xaml", UriKind.Relative) });
        Children.Add(choices);
        Caption("쪽 유형", 0);
        var pageGroup = Guid.NewGuid().ToString();
        var types = new Grid { Margin = new(0, 12, 0, 0), ToolTip = "예상 금액을 확인할 쪽 유형입니다. 실제 사용량은 변환에 성공한 각 쪽의 판정에 따라 계산됩니다." };
        types.ColumnDefinitions.Add(new()); types.ColumnDefinitions.Add(new() { Width = new(8) }); types.ColumnDefinitions.Add(new());
        choices.Children.Add(types);
        textPage = Choice(pageGroup, "텍스트 쪽", null, pricing is null ? "—" : $"{pricing.TextCredits} 크레딧");
        var visualPage = Choice(pageGroup, "그림 쪽", null, pricing is null ? "—" : $"{pricing.VisualCredits} 크레딧");
        types.Children.Add(textPage); types.Children.Add(visualPage); Grid.SetColumn(visualPage, 2);
        textPage.IsChecked = true;
        textPage.Checked += (_, _) => Refresh(); visualPage.Checked += (_, _) => Refresh();
        Caption("모델", 12);
        var modelGroup = Guid.NewGuid().ToString();
        var selected = pricing?.Models.FirstOrDefault(m => m.Code == initialModel)
            ?? pricing?.Models.FirstOrDefault(m => m.Code == "MINERU") ?? pricing?.Models.FirstOrDefault();
        foreach (var model in pricing?.Models ?? [])
        {
            var (level, name) = model.Code switch {
                "MINERU" => ("기본", "MinerU"), "HAIKU" => ("중급", "Haiku"),
                "SONNET" => ("고급", "Sonnet"), "OPUS" => ("최고급", "Opus"), _ => ("교열", model.Code) };
            var radio = Choice(modelGroup, level, name, $"쪽당 추가 +{model.ExtraCredits}");
            radio.Margin = new(0, model == pricing!.Models[0] ? 12 : 4, 0, 0);
            radio.Checked += (_, _) => { Model = model; Refresh(); ModelChanged?.Invoke(); };
            choices.Children.Add(radio);
            radio.IsChecked = model == selected;
        }
        var total = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 12, 0, 0), MinHeight = 20 };
        total.Children.Add(new TextBlock { Text = "예상 사용 크레딧", FontSize = 14, Margin = new(0, 0, 8, 0) });
        total.Children.Add(amount); Children.Add(total);
        Configure(null);
    }

    private void Caption(string text, int top) => choices.Children.Add(new TextBlock { Text = text, FontWeight = FontWeights.SemiBold,
        FontSize = 14, Height = 20, Foreground = EditorModalDesign.Color("#5F5F5F"), Margin = new(0, top, 0, 0) });

    private RadioButton Choice(string group, string label, string? name, string price)
    {
        var radio = new RadioButton { GroupName = group, Style = (Style)FindResource("ProofChoice") };
        var content = new DockPanel();
        var rate = new TextBlock { Text = price, VerticalAlignment = VerticalAlignment.Center,
            FontSize = name is null ? 14 : 12, FontWeight = name is null ? FontWeights.SemiBold : FontWeights.Normal };
        if (name is null)
        {
            radio.Checked += (_, _) => rate.Foreground = EditorModalDesign.Color("#003E92");
            radio.Unchecked += (_, _) => rate.ClearValue(TextBlock.ForegroundProperty);
        }
        DockPanel.SetDock(rate, Dock.Right); content.Children.Add(rate);
        var names = new StackPanel { Orientation = Orientation.Horizontal };
        names.Children.Add(new TextBlock { Text = label, FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
        if (name is not null) names.Children.Add(new TextBlock { Text = name, FontSize = 12, Margin = new(8, 0, 0, 0),
            Foreground = EditorModalDesign.Color("#8A8A8A"), VerticalAlignment = VerticalAlignment.Center });
        content.Children.Add(names); radio.Content = content;
        AutomationProperties.SetName(radio, name is null ? label : label + " " + name);
        AutomationProperties.SetHelpText(radio, price);
        return radio;
    }

    public void Configure(int? count, string? note = null, bool enabled = true)
    {
        pages = count; message = note; choices.IsEnabled = enabled && pricing is not null;
        Refresh();
    }

    private void Refresh()
    {
        if (message is not null) { amount.Text = message; return; }
        if (pricing is null || Model is null) { amount.Text = "요금표 조회 불가 · 서버 기본 교열"; return; }
        long rate = (textPage.IsChecked == true ? pricing.TextCredits : pricing.VisualCredits) + (long)Model.ExtraCredits;
        amount.Text = pages is int count ? $"{rate * count} 크레딧" : $"쪽당 {rate} 크레딧 · 쪽수 확인 후 계산";
    }
}
