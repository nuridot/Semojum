using System.Windows;
using System.Windows.Controls.Primitives;

namespace Semojum.Desktop.Controls;

public static class ShadowPopupPlacement
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(ShadowPopupPlacement), new PropertyMetadata(false, Changed));

    public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);

    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is Popup popup)
            popup.CustomPopupPlacementCallback = (bool)args.NewValue ? (size, targetSize, offset) => Place(popup, size, targetSize, offset) : null;
    }

    private static CustomPopupPlacement[] Place(Popup popup, Size size, Size target, Point offset)
    {
        // WPF supplies the child's rendered bounds in screen pixels. A submenu
        // and its parent can occupy monitors with different scaling.
        var marginX = 0d;
        var marginY = 0d;
        if (popup.Child is FrameworkElement child)
        {
            if (child.ActualWidth > 0) marginX = child.Margin.Left * size.Width / child.ActualWidth;
            if (child.ActualHeight > 0) marginY = child.Margin.Top * size.Height / child.ActualHeight;
        }
        return [
            new(new Point(target.Width - marginX, -marginY), PopupPrimaryAxis.Horizontal),
            new(new Point(-size.Width - marginX, -marginY), PopupPrimaryAxis.Horizontal)
        ];
    }
}
