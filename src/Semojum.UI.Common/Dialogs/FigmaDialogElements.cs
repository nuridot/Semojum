using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;
using static System.Windows.Controls.Control;
using Semojum.Desktop.Controls;
using SharpVectors.Converters;
using static System.Windows.FrameworkElement;
using static System.Windows.UIElement;

namespace Semojum.Desktop.Dialogs;

// Common Figma primitives shared by dialogs; screen-specific layout remains in each dialog.
public static class FigmaDialogElements
{
    public static Brush Brush(string value) => (Brush)new BrushConverter().ConvertFromString(value)!;
    public static SvgViewbox DesignIcon(string name, double width, double height, string assemblyName = "Semojum.Mockup") => new()
    {
        Source = new Uri($"pack://application:,,,/{assemblyName};component/Assets/Figma/{name}"),
        Width = width, Height = height, Stretch = Stretch.None
    };
    public static ControlTemplate SurfaceTemplate(double radius, bool stretchContent = false)
        => SurfaceTemplate(radius, stretchContent, useControlInsets: false);

    public static ControlTemplate SurfaceTemplate(double radius, bool stretchContent, bool useControlInsets)
    {
        var root = new FrameworkElementFactory(typeof(Grid));
        var outer = new FrameworkElementFactory(typeof(Border), "FocusOuter"); outer.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius + 3));
        outer.SetValue(Border.BorderThicknessProperty, new Thickness(2)); outer.SetValue(Border.BorderBrushProperty, Brush("#1A1A1A")); outer.SetValue(MarginProperty, new Thickness(-3)); outer.SetValue(VisibilityProperty, Visibility.Collapsed); outer.SetValue(IsHitTestVisibleProperty, false); root.AppendChild(outer);
        var inner = new FrameworkElementFactory(typeof(Border), "FocusInner"); inner.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius + 1)); inner.SetValue(Border.BorderThicknessProperty, new Thickness(1)); inner.SetValue(Border.BorderBrushProperty, Brushes.White); inner.SetValue(MarginProperty, new Thickness(-1)); inner.SetValue(VisibilityProperty, Visibility.Collapsed); inner.SetValue(IsHitTestVisibleProperty, false); root.AppendChild(inner);
        var border = new FrameworkElementFactory(typeof(Border), "Surface"); border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
        if (useControlInsets)
        {
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(PaddingProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(BorderThicknessProperty));
        }
        else border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetBinding(Border.BackgroundProperty, FigmaButtonStateBrush.Binding());
        border.SetBinding(Border.BorderBrushProperty, FigmaButtonStateBrush.Binding(true));
        var content = new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(HorizontalAlignmentProperty, stretchContent ? HorizontalAlignment.Stretch : HorizontalAlignment.Center);
        content.SetBinding(System.Windows.Documents.TextElement.ForegroundProperty, FigmaButtonForegroundBrush.Binding());
        content.SetBinding(OpacityProperty, FigmaDisabledIconOpacity.Binding());
        content.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(content);
        root.AppendChild(border);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = root };
        var focus = new Trigger { Property = IsKeyboardFocusedProperty, Value = true };
        focus.Setters.Add(new Setter(VisibilityProperty, Visibility.Visible, "FocusOuter")); focus.Setters.Add(new Setter(VisibilityProperty, Visibility.Visible, "FocusInner")); template.Triggers.Add(focus);
        return template;
    }
    public static Grid NumberField(TextBox box, FrameworkElement entry, string label, Action<int> step)
    {
        var number = new Grid { Width = 96, Height = 32 };
        number.Children.Add(entry);
        box.Padding = new(10, 0, 25, 0);
        var arrows = new StackPanel { Width = 20, VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 0, 5, 0) };
        number.Children.Add(arrows);
        foreach (var delta in new[] { 1, -1 })
        {
            var icon = delta > 0 ? EditorModalDesign.Icon("layout-step-up", 7.20047, 3.89951, "Semojum.UI.Common")
                : EditorModalDesign.Icon("layout-step-down", 12, 12, "Semojum.UI.Common");
            if (delta > 0) { icon.RenderTransformOrigin = new(.5, .5); icon.RenderTransform = new ScaleTransform(1, -1); }
            var button = new RepeatButton { Content = icon, Width = 20, Height = 12, MinHeight = 0, MinWidth = 0,
                Padding = new(0), Margin = new(0), Focusable = false, Background = Brushes.Transparent, BorderThickness = new(0),
                Template = EditorModalDesign.ButtonTemplate(typeof(RepeatButton)) };
            AutomationProperties.SetName(button, label + (delta > 0 ? " 늘리기" : " 줄이기"));
            button.Click += (_, _) => step(delta);
            arrows.Children.Add(button);
        }
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key is not (Key.Up or Key.Down) || Keyboard.Modifiers != ModifierKeys.None) return;
            step(e.Key == Key.Up ? 1 : -1); e.Handled = true;
        };
        return number;
    }

    public static Style ScopeStyle()
    {
        var border = new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetBinding(Border.BackgroundProperty, new Binding(nameof(Control.Background)) { RelativeSource = RelativeSource.TemplatedParent });
        border.SetBinding(Border.BorderBrushProperty, new Binding(nameof(Control.BorderBrush)) { RelativeSource = RelativeSource.TemplatedParent });
        var text = new FrameworkElementFactory(typeof(ContentPresenter)); text.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        text.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(text);
        var style = new Style(typeof(ListBoxItem)); style.Setters.Add(new Setter(TemplateProperty, new ControlTemplate(typeof(ListBoxItem)) { VisualTree = border }));
        style.Setters.Add(new Setter(BackgroundProperty, Brush("#FBFBFB"))); style.Setters.Add(new Setter(BorderBrushProperty, Brush("#EAEAEA")));
        style.Setters.Add(new Setter(ForegroundProperty, Brush("#5F5F5F")));
        var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(BackgroundProperty, Brush("#EFF6FC"))); selected.Setters.Add(new Setter(BorderBrushProperty, Brush("#005FB8")));
        selected.Setters.Add(new Setter(ForegroundProperty, Brush("#003E92"))); style.Triggers.Add(selected);
        var disabled = new Trigger { Property = IsEnabledProperty, Value = false }; disabled.Setters.Add(new Setter(OpacityProperty, .5)); style.Triggers.Add(disabled);
        return style;
    }
}
