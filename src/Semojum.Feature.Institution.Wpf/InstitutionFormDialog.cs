using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;

namespace Semojum.Desktop.Dialogs;

internal abstract class InstitutionFormDialog : CompactDialog
{
    protected readonly StackPanel Fields = new() { Margin = new(24, 0, 24, 8) };
    protected readonly DockPanel FooterBar = new() { Height = 64, Margin = new(24, 0, 24, 0), LastChildFill = false };
    protected readonly StackPanel FooterRight = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    protected static Brush Ink(string hex) => FigmaDialogElements.Brush(hex);

    protected InstitutionFormDialog(string title, double width, double height, string closeIcon) : base(title, width, height, popover: true)
    {
        if (Content is Border frame) { frame.Background = Ink("#F3F3F3"); frame.BorderBrush = Ink("#EAEAEA"); frame.CornerRadius = new(8); }
        WindowChrome.GetWindowChrome(this).CaptionHeight = 56;
        var header = new DockPanel { Height = 56, Margin = new(24, 0, 16, 0) };
        var close = new Button { Width = 28, Height = 28, MinWidth = 0, MinHeight = 0, Margin = new(0), Padding = new(0),
            Background = Brushes.Transparent, BorderThickness = new(0), Template = EditorModalDesign.ButtonTemplate(typeof(Button)),
            Content = FigmaDialogElements.DesignIcon(closeIcon, 16, 16, "Semojum.Feature.Institution.Wpf") };
        AutomationProperties.SetName(close, "닫기"); close.ToolTip = "닫기"; close.Click += (_, _) => Close();
        WindowChrome.SetIsHitTestVisibleInChrome(close, true); DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
        header.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        DockPanel.SetDock(header, Dock.Top); Root.Children.Add(header);
        DockPanel.SetDock(FooterRight, Dock.Right); FooterBar.Children.Add(FooterRight);
        DockPanel.SetDock(FooterBar, Dock.Bottom); Root.Children.Add(FooterBar);
        Body(new ScrollViewer { Content = Fields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
    }

    protected Button FooterAction(string label, Func<Task> action, bool primary = false, bool left = false, double width = 88)
    {
        var button = Button(label, action, primary);
        button.Width = width; button.VerticalAlignment = VerticalAlignment.Center;
        button.Margin = !left && FooterRight.Children.Count > 0 ? new(8, 0, 0, 0) : new(0);
        if (left) { DockPanel.SetDock(button, Dock.Left); FooterBar.Children.Add(button); }
        else FooterRight.Children.Add(button);
        return button;
    }

    protected void Dismiss(string label, bool primary = false) => FooterAction(label, () => { Close(); return Task.CompletedTask; }, primary);

    protected StackPanel Card()
    {
        var rows = new StackPanel();
        Fields.Children.Add(new Border { Background = Ink("#F9F9F9"), CornerRadius = new(8), Padding = new(16, 12, 16, 12),
            Margin = new(0, Fields.Children.Count == 0 ? 0 : 16, 0, 0), Child = rows });
        return rows;
    }

    protected static void Field(Panel card, string label, FrameworkElement field, bool readOnly = false, bool alignTop = false)
    {
        var row = new Grid { MinHeight = readOnly ? 28 : 20, Margin = new(0, card.Children.Count == 0 ? 0 : 12, 0, 0) };
        row.ColumnDefinitions.Add(new() { Width = new(readOnly ? 132 : 162) }); row.ColumnDefinitions.Add(new());
        row.Children.Add(new TextBlock { Text = label, FontSize = 14, VerticalAlignment = alignTop ? VerticalAlignment.Top : VerticalAlignment.Center,
            Foreground = Ink(readOnly ? "#5F5F5F" : "#1A1A1A") });
        AutomationProperties.SetName(field, label); Grid.SetColumn(field, 1); row.Children.Add(field); card.Children.Add(row);
    }
    protected static TextBlock Value(string text) => new() { Text = text, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };

    protected static TextBox Entry(string label, int maxLength)
    {
        var box = new TextBox { FontSize = 14, MaxLength = maxLength, Height = 32, Padding = new(12, 0, 8, 0), Background = Brushes.White, BorderBrush = Ink("#D1D1D1") };
        AutomationProperties.SetName(box, label); return box;
    }

    protected static FrameworkElement Quantity(TextBox box, long minimum, long maximum)
    {
        var line = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        var number = new Grid { Width = 96, Height = 32 }; line.Children.Add(number); number.Children.Add(box);
        box.Padding = new(12, 0, 22, 0);
        var spin = new StackPanel { Width = 20, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 4, 0) };
        number.Children.Add(spin);
        void Step(int delta, string asset, string label)
        {
            var icon = FigmaDialogElements.DesignIcon(asset, 12, 12, "Semojum.Feature.Institution.Wpf");
            if (delta > 0) { icon.RenderTransformOrigin = new(.5,.5); icon.RenderTransform = new ScaleTransform(1,-1); }
            var button = new Button { Content = icon, Width = 20, Height = 12, MinWidth = 0, MinHeight = 0, Margin = new(0), Padding = new(0),
                Background = Brushes.Transparent, BorderThickness = new(0), Template = EditorModalDesign.ButtonTemplate(typeof(Button)) };
            AutomationProperties.SetName(button, label); button.Click += (_, _) =>
            {
                long.TryParse(box.Text, NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var value);
                value = Math.Clamp(value, 0, maximum);
                var next = delta > 0 ? (value == maximum ? maximum : value + 1) : Math.Max(minimum, value - 1);
                box.Text = next.ToString("N0", CultureInfo.CurrentCulture);
            };
            spin.Children.Add(button);
        }
        Step(1, "institution-credit-spin-up.svg", "크레딧 1 증가"); Step(-1, "institution-credit-spin-down.svg", "크레딧 1 감소");
        line.Children.Add(new TextBlock { Text = "크레딧", FontSize = 12, Foreground = Ink("#5F5F5F"), Margin = new(8, 0, 0, 0) });
        return line;
    }
}
