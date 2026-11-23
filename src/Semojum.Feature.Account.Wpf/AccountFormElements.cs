using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;
using SharpVectors.Converters;

namespace Semojum.Desktop.Dialogs;

internal static class AccountFormElements
{
    internal static Brush Ink(string color) => FigmaDialogElements.Brush(color);

    internal static void StyleCard(Border card)
    {
        card.Width = 420;
        card.Padding = new(60, 40, 60, 40);
        card.BorderThickness = new(1);
        card.BorderBrush = Ink("#EAEAEA");
        card.Background = Brushes.White;
        card.CornerRadius = new(8);
        card.Effect = new DropShadowEffect { Direction = 270, BlurRadius = 18, ShadowDepth = 16, Opacity = .18 };
    }

    internal static SvgViewbox Logo()
    {
        var logo = new SvgViewbox { Width = 200, Height = 48, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center,
            Source = new Uri("pack://application:,,,/Semojum.UI.Common;component/Assets/Brand/nuridot-logo.svg") };
        AutomationProperties.SetName(logo, "누리닷 로고");
        return logo;
    }

    internal static FrameworkElement Field(string label, Control input)
    {
        var field = new StackPanel { Margin = new(0,16,0,0) };
        field.Children.Add(new TextBlock { Text = label, Height = 16, FontSize = 12, Foreground = Ink("#5F5F5F"), VerticalAlignment = VerticalAlignment.Center });
        input.Padding = new(15,0,15,0); input.FontSize = 14; input.Background = Brushes.Transparent;
        input.BorderThickness = new(0); input.VerticalContentAlignment = VerticalAlignment.Center;
        AutomationProperties.SetName(input, label);
        var surface = new Border { Height = 40, Margin = new(0,4,0,0), CornerRadius = new(4), Child = input };
        var style = new Style(typeof(Border));
        style.Setters.Add(new Setter(Border.BackgroundProperty, Ink("#F9F9F9")));
        style.Setters.Add(new Setter(Border.BorderBrushProperty, Ink("#EAEAEA")));
        style.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1)));
        var focus = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
        focus.Setters.Add(new Setter(Border.BackgroundProperty, Brushes.White)); focus.Setters.Add(new Setter(Border.BorderBrushProperty, Ink("#005FB8")));
        focus.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(0,0,0,2))); style.Triggers.Add(focus); surface.Style = style;
        field.Children.Add(surface);
        return field;
    }

    internal static Button ActionButton(object label, bool primary)
    {
        var button = new Button { Content = label, MinWidth = 0, MinHeight = 0, Height = primary ? 40 : 32,
            FontSize = primary ? 14 : 12, FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = primary ? Brushes.White : Ink("#1A1A1A"), Background = primary ? Ink("#005FB8") : Brushes.White,
            BorderBrush = primary ? Ink("#005FB8") : Ink("#EAEAEA"), BorderThickness = new(1), Padding = new(0), Margin = new(0),
            Template = FigmaDialogElements.SurfaceTemplate(4) };
        return button;
    }

    internal static void StyleFeedback(TextBlock feedback)
    {
        feedback.TextWrapping = TextWrapping.Wrap; feedback.Foreground = Ink("#C42B1C"); feedback.Margin = new(0,12,0,0);
        AutomationProperties.SetLiveSetting(feedback, AutomationLiveSetting.Polite);
        var style = new Style(typeof(TextBlock));
        var empty = new DataTrigger { Binding = new Binding("Text") { RelativeSource = RelativeSource.Self }, Value = "" };
        empty.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed)); style.Triggers.Add(empty); feedback.Style = style;
    }

    internal static void SetFeedback(TextBlock feedback, string message, bool information = false)
    {
        feedback.Foreground = Ink(information ? "#5F5F5F" : "#C42B1C");
        feedback.Text = message;
    }

    internal static Style CheckStyle()
    {
        var style = new Style(typeof(CheckBox), PublicDialogDesign.ConsentStyle("Semojum.Feature.Account.Wpf", "login-check.svg", 12));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0)));
        style.Setters.Add(new Setter(FrameworkElement.HeightProperty, 20d)); style.Setters.Add(new Setter(Control.FontSizeProperty, 14d));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        return style;
    }
}
