using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Semojum.Desktop.Controls;

public partial class LineTranslation : UserControl
{
    public event EventHandler? CloseRequested;
    private int sourceLine;
    private bool showingPrint;

    public LineTranslation()
    {
        InitializeComponent();
        UpdateNavigation();
    }

    public void SetPreview(int line, string text, bool reverse = false)
    {
        if (line == sourceLine && text == Cells.Text && reverse == showingPrint) return;
        sourceLine = line;
        showingPrint = reverse;
        LineLabel.Text = reverse ? "줄 역점역" : "줄 점역";
        LineLabel.ToolTip = line > 0 ? $"{(reverse ? "점자" : "텍스트")} {line}줄" : LineLabel.Text;
        Cells.SetResourceReference(TextBlock.FontFamilyProperty, reverse ? "InterfaceFont" : "BraillePlainFont");
        var unit = reverse ? "텍스트 줄" : "점자 줄";
        NextLine.ToolTip = "다음 " + unit;
        PreviousLine.ToolTip = "이전 " + unit;
        System.Windows.Automation.AutomationProperties.SetName(NextLine, "다음 " + unit);
        System.Windows.Automation.AutomationProperties.SetName(PreviousLine, "이전 " + unit);
        Cells.Text = text;
        Viewport.ScrollToTop();
    }

    private void UpdateNavigation()
    {
        PreviousLine.IsEnabled = Viewport.VerticalOffset > 0.5;
        NextLine.IsEnabled = Viewport.ScrollableHeight - Viewport.VerticalOffset > 0.5;
    }

    private void Viewport_ScrollChanged(object sender, ScrollChangedEventArgs e) => UpdateNavigation();
    private void NextLine_Click(object sender, RoutedEventArgs e) => Viewport.ScrollToVerticalOffset(Viewport.VerticalOffset + Cells.LineHeight);
    private void PreviousLine_Click(object sender, RoutedEventArgs e) => Viewport.ScrollToVerticalOffset(Viewport.VerticalOffset - Cells.LineHeight);
    private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta == 0 || !(e.Delta > 0 ? PreviousLine.IsEnabled : NextLine.IsEnabled)) return;
        Viewport.ScrollToVerticalOffset(Viewport.VerticalOffset - Math.Sign(e.Delta) * Cells.LineHeight);
        e.Handled = true;
    }
    private void Close_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
}
