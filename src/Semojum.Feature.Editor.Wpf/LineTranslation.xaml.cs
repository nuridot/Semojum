using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Semojum.Desktop.Controls;

public partial class LineTranslation : UserControl
{
    public event EventHandler? CloseRequested;
    private int sourceLine;

    public LineTranslation()
    {
        InitializeComponent();
        UpdateNavigation();
    }

    public void SetPreview(int line, string cells)
    {
        if (line == sourceLine && cells == Cells.Text) return;
        sourceLine = line;
        LineLabel.ToolTip = line > 0 ? $"텍스트 {line}줄의 점역 미리보기" : "점역 미리보기";
        Cells.Text = cells;
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
