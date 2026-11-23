using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SharpVectors.Converters;

namespace Semojum.Feature.Editor.Wpf;

/// <summary>Navigation for actual page identities; missing and unavailable pages are never invented.</summary>
public sealed class PanelPageBar : UserControl
{
    private readonly Button previous = new(), next = new();
    private readonly TextBox input = new();
    private readonly TextBlock total = new();
    private readonly StackPanel zoom = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Button zoomOut = new(), zoomIn = new(), percent = new();
    private int[] pages = [];
    private HashSet<int> available = [];
    private int? current;
    private string stateKey = "";
    public event Action<int>? PageRequested;
    public event Action? InvalidPageRequested;
    public PanelPageBar()
    {
        Height = 24;
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/Semojum.UI.Common;component/Controls/FormEntry.xaml", UriKind.Relative) });
        var layout = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Content = layout;
        ConfigureButton(previous); ConfigureButton(next);
        System.Windows.Automation.AutomationProperties.SetName(previous, "이전 쪽");
        System.Windows.Automation.AutomationProperties.SetName(next, "다음 쪽");
        previous.Click += (_, _) => Step(-1); next.Click += (_, _) => Step(1);
        layout.Children.Add(previous);
        input.Width = 40; input.Height = 24; input.MinHeight = 24; input.Padding = new Thickness(2, 0, 2, 0);
        input.Margin = new Thickness(4, 0, 4, 0); input.FontSize = 12; input.TextAlignment = TextAlignment.Center;
        input.VerticalContentAlignment = VerticalAlignment.Center;
        input.Template = (ControlTemplate)FindResource("LayoutEntry");
        input.BorderThickness = new Thickness(1); input.BorderBrush = Brush("#D1D1D1"); input.Background = Brushes.White;
        System.Windows.Automation.AutomationProperties.SetName(input, "이동할 쪽 번호");
        input.KeyDown += (_, e) => { if (e.Key != Key.Enter) return; e.Handled = true; if (int.TryParse(input.Text, out var page) && available.Contains(page)) PageRequested?.Invoke(page); else { InvalidPageRequested?.Invoke(); input.Text = current?.ToString() ?? "—"; } };
        input.LostKeyboardFocus += (_, _) => input.Text = current?.ToString() ?? "—";
        layout.Children.Add(input);
        total.FontSize = 12; total.Foreground = Brush("#5F5F5F"); total.VerticalAlignment = VerticalAlignment.Center; total.Margin = new Thickness(0, 0, 4, 0);
        layout.Children.Add(total); layout.Children.Add(next); layout.Children.Add(zoom);
        ConfigureButton(zoomOut); ConfigureButton(zoomIn); ConfigureButton(percent);
        zoomOut.Content = Icon("panel-zoom-minus.svg"); zoomIn.Content = Icon("panel-zoom-plus.svg");
        percent.Width = double.NaN; percent.MinWidth = 30; percent.FontSize = 12; percent.FontWeight = FontWeights.Normal; percent.Foreground = Brush("#5F5F5F"); percent.Margin = new Thickness(4, 0, 4, 0); percent.Content = "100%";
        zoom.Children.Add(zoomOut); zoom.Children.Add(percent); zoom.Children.Add(zoomIn);
    }
    public void ConfigureZoom(string pane, Action<int> step, Action<Button> choose)
    {
        zoom.Visibility = Visibility.Visible;
        foreach (var (control, name) in new[] { (zoomOut, pane + " 축소"), (percent, pane + " 확대 비율"), (zoomIn, pane + " 확대") })
        {
            System.Windows.Automation.AutomationProperties.SetName(control, name);
            control.ToolTip = name;
        }
        zoomOut.Click += (_, _) => step(-1); zoomIn.Click += (_, _) => step(1); percent.Click += (_, _) => choose(percent);
    }
    public void SetCompact(bool compact) => zoomOut.Visibility = zoomIn.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
    public void SetZoomPercent(string label) => percent.Content = label;
    public void SetIcons(string left, string right)
    {
        previous.Content = Icon(left); next.Content = Icon(right);
    }
    public void SetPages(IEnumerable<int> pageNumbers, IEnumerable<int> navigable, int? selected, int? confirmedTotal = null, bool inferTotal = true)
    {
        var actual = pageNumbers.Distinct().Order().ToArray(); var allowed = navigable.ToHashSet();
        var key = string.Join(",", actual) + "|" + string.Join(",", allowed.Order()) + "|" + selected + "|" + confirmedTotal + "|" + inferTotal;
        if (key == stateKey) return;
        stateKey = key; pages = actual; available = allowed; current = selected is { } p && actual.Contains(p) ? p : null;
        if (!input.IsKeyboardFocusWithin) input.Text = current?.ToString() ?? "—";
        input.IsEnabled = allowed.Count > 0;
        var continuous = pages.SequenceEqual(Enumerable.Range(1, pages.Length));
        var count = confirmedTotal ?? (inferTotal && continuous && pages.Length > 0 ? pages.Length : (int?)null);
        total.Text = count is { } known ? "/ " + known : "/ —";
        total.ToolTip = !count.HasValue && pages.Length > 0 ? $"수신한 쪽 {pages.Length}개 · 전체 쪽수 미확인" : null;
        var index = current is null ? -1 : Array.IndexOf(pages, current.Value);
        previous.IsEnabled = index > 0 && available.Contains(pages[index - 1]);
        next.IsEnabled = index >= 0 && index + 1 < pages.Length && available.Contains(pages[index + 1]);
    }
    private void Step(int delta)
    {
        if (current is null) return;
        var index = Array.IndexOf(pages, current.Value) + delta;
        if (index >= 0 && index < pages.Length && available.Contains(pages[index])) PageRequested?.Invoke(pages[index]);
    }
    private static void ConfigureButton(Button button)
    {
        button.Width = button.MinWidth = 24; button.Height = button.MinHeight = 24; button.Margin = new Thickness(0); button.Padding = new Thickness(0);
        button.Background = Brushes.Transparent; button.BorderBrush = Brushes.Transparent; button.BorderThickness = new Thickness(0);
    }
    private static SvgViewbox Icon(string file) => new() { Source = new Uri($"pack://application:,,,/Semojum.Feature.Editor.Wpf;component/Assets/Figma/{file}"), Width = 16, Height = 16, Stretch = Stretch.None, IsHitTestVisible = false };
    private static SolidColorBrush Brush(string color) => (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
}
