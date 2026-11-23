using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Semojum.Desktop.Dialogs;

public sealed partial class InstitutionWindow
{
    internal StackPanel LayoutStats { get; } = new();
    internal StackPanel AccountStats { get; } = new();
    private ScrollViewer AccountStatsScroll => new() { Content = AccountStats, MaxHeight = 136, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };

    private static Button DashboardButton(string label, int width, bool primary = false)
    {
        var button = new Button { Content = label, Height = 32, MinWidth = 0, Padding = new(8, 0, 8, 0), Margin = new(0),
            Background = primary ? FigmaDialogElements.Brush("#005FB8") : FigmaDialogElements.Brush("#FBFBFB"), Foreground = primary ? Brushes.White : FigmaDialogElements.Brush("#1A1A1A"),
            BorderBrush = FigmaDialogElements.Brush(primary ? "#005FB8" : "#D1D1D1"), BorderThickness = new(1), Template = FigmaDialogElements.SurfaceTemplate(4) };
        if (width > 0) button.Width = width;
        return button;
    }

    private static Border DashboardCard(string title, FrameworkElement? action, bool summary, params FrameworkElement[] elements)
    {
        var panel = new StackPanel();
        var heading = new DockPanel();
        if (action is not null) { action.VerticalAlignment = VerticalAlignment.Center; DockPanel.SetDock(action, Dock.Right); heading.Children.Add(action); }
        heading.Children.Add(new TextBlock { Text = title, FontSize = summary ? 12 : 14, FontWeight = summary ? FontWeights.Normal : FontWeights.SemiBold,
            LineHeight = summary ? 16 : 20, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, VerticalAlignment = VerticalAlignment.Center,
            Foreground = FigmaDialogElements.Brush(summary ? "#5F5F5F" : "#1A1A1A") });
        panel.Children.Add(heading);
        foreach (var element in elements) { element.Margin = new(0, 8, 0, 0); panel.Children.Add(element); }
        var card = new Border { Background = Brushes.White, BorderBrush = FigmaDialogElements.Brush("#EAEAEA"), BorderThickness = new(1), CornerRadius = new(8),
            Padding = new(16,12,16,12), VerticalAlignment = VerticalAlignment.Top, Child = panel };
        AutomationProperties.SetName(card, title);
        return card;
    }

    private static Grid CardRow(params Border[] cards)
    {
        var grid = new Grid { Margin = new(0, 0, 0, 16) };
        foreach (var card in cards) grid.Children.Add(card);
        bool? stacked = null;
        void Arrange(bool narrow)
        {
            if (stacked == narrow) return;
            stacked = narrow; grid.ColumnDefinitions.Clear(); grid.RowDefinitions.Clear();
            for (var i = 0; i < cards.Length; i++)
            {
                if (narrow) grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
                else { if (i > 0) grid.ColumnDefinitions.Add(new() { Width = new(16) }); grid.ColumnDefinitions.Add(new()); }
                Grid.SetRow(cards[i], narrow ? i : 0); Grid.SetColumn(cards[i], narrow ? 0 : 2 * i);
                cards[i].Margin = narrow && i > 0 ? new(0, 16, 0, 0) : new(0);
            }
        }
        Arrange(false); grid.SizeChanged += (_, e) => Arrange(e.NewSize.Width < (cards.Length == 3 ? 900 : 850));
        return grid;
    }

    private Border MakeMonthlyCard()
    {
        ChartArea.Height = 140;
        ChartArea.ColumnDefinitions.Add(new() { Width = new(40) }); ChartArea.ColumnDefinitions.Add(new() { Width = new(8) }); ChartArea.ColumnDefinitions.Add(new());
        var axis = new Canvas(); ChartArea.Children.Add(axis);
        for (int i = 0; i < ChartTicks.Length; i++)
        {
            ChartTicks[i].Width = 40; Canvas.SetTop(ChartTicks[i], 8 + i * 104d / 5); axis.Children.Add(ChartTicks[i]);
        }
        ChartScroll.Content = Chart; Grid.SetColumn(ChartScroll, 2); ChartArea.Children.Add(ChartScroll); ChartScroll.SizeChanged += (_, _) => SizeChartColumns();
        var plot = new Grid(); plot.Children.Add(ChartArea); plot.Children.Add(ChartEmpty);
        return DashboardCard("월별 크레딧", ChartAverage, false, plot);
    }

    private static TextBlock StatLabel(string text) => new() { Text = text, FontSize = 12, LineHeight = 16, LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
        TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, ToolTip = text };

    internal void RenderLayoutUsage(IReadOnlyList<InstitutionLayoutUsage>? usage)
    {
        LayoutStats.Children.Clear();
        string[] labels = ["본문", "표", "수식", "그림", "기타"];
        string[] colors = ["#005FB8", "#5B8DEF", "#9FC0F3", "#D6E4FA", "#737373"];
        var amounts = new decimal[labels.Length];
        foreach (var entry in usage ?? [])
        {
            var i = entry.LayoutType switch { "PAGE_LAYOUT_TEXT" => 0, "PAGE_LAYOUT_TABLE" => 1, "PAGE_LAYOUT_FORMULA" => 2, "PAGE_LAYOUT_VISUAL" => 3, _ => 4 };
            if (entry.Credits > 0) amounts[i] += entry.Credits;
        }
        var total = amounts.Sum();
        if (total == 0) { LayoutStats.Children.Add(StatLabel(usage is null ? "유형별 사용량 정보 없음" : "최근 6개월간 차감 내역이 없습니다.")); return; }
        var bar = new Grid { Height = 16, ToolTip = "최근 6개월간 유형별 차감 크레딧" };
        LayoutStats.Children.Add(bar);
        for (var i = 0; i < (amounts[4] > 0 ? 5 : 4); i++)
        {
            var brush = FigmaDialogElements.Brush(colors[i]);
            var hint = $"{labels[i]} · {amounts[i]:N0} 크레딧";
            bar.ColumnDefinitions.Add(new() { Width = new((double)(amounts[i] / total), GridUnitType.Star) });
            var segment = new Border { Background = brush, ToolTip = hint }; Grid.SetColumn(segment, i); bar.Children.Add(segment);
            var row = new DockPanel { Height = 24, Margin = new(0,8,0,0), ToolTip = hint };
            var percentage = StatLabel($"{amounts[i] / total:P0}".Replace(" ", "")); percentage.FontWeight = FontWeights.SemiBold; percentage.FontSize = 14;
            DockPanel.SetDock(percentage, Dock.Right); row.Children.Add(percentage);
            row.Children.Add(new Border { Width = 10, Height = 10, Background = brush, CornerRadius = new(5), Margin = new(0,0,8,0), VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(StatLabel(labels[i])); LayoutStats.Children.Add(row);
        }
    }

    private void RenderAccountUsage(IReadOnlyList<InstitutionAccountRow>? members)
    {
        AccountStats.Children.Clear();
        if (members is null || members.Count == 0) { AccountStats.Children.Add(StatLabel(members is null ? "계정별 사용량 정보 없음" : "소속 계정이 없습니다.")); return; }
        var ranked = members.OrderByDescending(m => m.UsedCredits).ThenBy(m => m.LoginId, StringComparer.Ordinal).ToArray();
        var maximum = Math.Max(1, ranked[0].UsedCredits ?? 0);
        foreach (var member in ranked)
        {
            var row = new StackPanel { Margin = new(0, AccountStats.Children.Count == 0 ? 0 : 8, 0, 0) };
            var header = new DockPanel();
            var amount = StatLabel(member.UsedCredits?.ToString("N0") ?? "—"); DockPanel.SetDock(amount, Dock.Right); header.Children.Add(amount);
            header.Children.Add(StatLabel(string.IsNullOrWhiteSpace(member.Alias) ? member.LoginId : $"{member.LoginId} · {member.Alias}")); row.Children.Add(header);
            if (member.UsedCredits is long used)
            {
                var bar = new ProgressBar { Height = 8, Minimum = 0, Maximum = maximum, Value = used, Margin = new(0,4,0,0), Template = UsageBarTemplate() };
                AutomationProperties.SetName(bar, $"{member.LoginId} 이달 사용 {used:N0}"); row.Children.Add(bar);
            }
            AccountStats.Children.Add(row);
        }
    }

    internal static string Forecast(InstitutionDashboard value)
    {
        if (value.ChargedContractPages is null || value.RecentContractCredits is null) return "예측 정보 없음";
        if (value.ChargedContractPages is not > 0 || value.RecentContractCredits is not > 0 || value.CreditRemaining < 0 || value.ChargedContractPages > value.RecentContractCredits) return "예측할 사용 이력이 없습니다.";
        var ratio = (decimal)value.CreditRemaining / value.RecentContractCredits.Value;
        var pages = (System.Numerics.BigInteger)value.CreditRemaining * value.ChargedContractPages.Value / value.RecentContractCredits.Value;
        return $"이 추세면 약 {pages:N0}쪽 · {ratio * 6:N1}개월 더 처리";
    }
}
