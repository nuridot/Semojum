using System.Globalization;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;

namespace Semojum.Desktop.Dialogs;

public abstract class FormDialogBase : CompactDialog
{
    protected readonly StackPanel Fields = new();
    protected ScrollViewer FormBody { get; }
    protected Button HeaderClose { get; }
    protected FormDialogBase(string title, double width, double height, FrameworkElement? closeIcon = null) : base(title, width, height, popover: true)
    {
        UseLayoutRounding = true;
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/Semojum.UI.Common;component/Controls/FormEntry.xaml", UriKind.Relative) });
        var border = (Border)Content;
        border.CornerRadius = new(8); border.BorderBrush = EditorModalDesign.Color("#EAEAEA");
        border.Background = Background = EditorModalDesign.Color("#F3F3F3");
        var footer = Root.Children.OfType<Border>().Single();
        footer.Visibility = Visibility.Visible; footer.BorderThickness = new(0);
        Actions.Margin = new(24, 16, 24, 16);
        Status.Margin = new(24, 0, 24, 0);
        var header = new DockPanel { Height = 56, Margin = new(24, 0, 16, 0) };
        var close = HeaderClose = Button("닫기", () => { Close(); return Task.CompletedTask; });
        close.MinWidth = close.MinHeight = 0; close.Width = close.Height = 28;
        close.Content = closeIcon ?? EditorModalDesign.Icon("modal-close"); close.ToolTip = "닫기"; AutomationProperties.SetName(close, "닫기");
        close.Background = Brushes.Transparent; close.BorderThickness = new(0); close.Padding = close.Margin = new(0);
        close.Template = EditorModalDesign.ButtonTemplate(typeof(Button));
        WindowChrome.SetIsHitTestVisibleInChrome(close, true);
        WindowChrome.GetWindowChrome(this).CaptionHeight = 56;
        DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
        header.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        DockPanel.SetDock(header, Dock.Top); Root.Children.Add(header);
        FormBody = new ScrollViewer { Content = Fields, Padding = new(24, 0, 24, 8), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Body(FormBody);
    }

    protected void UseControlBorder()
    {
        var buttonStyle = new Style(typeof(Button), (Style)FindResource(typeof(Button)));
        buttonStyle.Setters.Add(new Setter(Control.BorderBrushProperty, EditorModalDesign.Color("#D1D1D1")));
        Resources[typeof(Button)] = buttonStyle;
    }

    protected void AlignLabelBaseline()
    {
        // Pretendard labels in these form frames sit one DIP above WPF's default baseline.
        var textStyle = new Style(typeof(TextBlock), (Style)FindResource(typeof(TextBlock)));
        textStyle.Setters.Add(new Setter(RenderTransformProperty, new TranslateTransform(0, -1)));
        Resources[typeof(TextBlock)] = textStyle;
    }

    protected Button FooterButton(string label, Func<Task> action, bool primary = false) => StyleFooter(Action(label, action, primary));
    protected Button FooterButton(string label, Action action, bool primary = false) => StyleFooter(Action(label, action, primary));

    protected Button LeadingFooterButton(string name, Func<Task> action, double width = 88)
    {
        var button = FooterButton(name, action);
        var footer = (Border)Actions.Parent;
        Actions.Children.Remove(button);
        footer.Child = null;
        var row = new DockPanel { Margin = Actions.Margin };
        Actions.Margin = new(0);
        button.Margin = new(0);
        button.MinWidth = button.Width = width;
        DockPanel.SetDock(button, Dock.Left);
        row.Children.Add(button);
        row.Children.Add(Actions);
        footer.Child = row;
        return button;
    }

    protected static FrameworkElement SelectField(ComboBox select, string name, FrameworkElement icon)
    {
        AutomationProperties.SetName(select, name);
        select.Height = 32; select.MinHeight = 0; select.FontSize = 14;
        select.Padding = new(13, 0, 28, 0); select.Background = Brushes.White; select.Foreground = EditorModalDesign.Color("#1A1A1A");
        select.VerticalContentAlignment = VerticalAlignment.Center;
        select.BorderBrush = EditorModalDesign.Color("#D1D1D1");
        var wrapper = new Grid { Height = 32, VerticalAlignment = VerticalAlignment.Center };
        wrapper.Children.Add(select);
        wrapper.Children.Add(new Border { Width = 32, Margin = new(0, 2, 1, 0), HorizontalAlignment = HorizontalAlignment.Right,
            Background = Brushes.White, IsHitTestVisible = false, Child = icon });
        wrapper.Children.Add(new Border { BorderBrush = select.BorderBrush, BorderThickness = new(1), CornerRadius = new(4), IsHitTestVisible = false });
        return wrapper;
    }

    private static Button StyleFooter(Button button)
    {
        button.MinWidth = button.Width = 88; button.Margin = new(8, 0, 0, 0);
        button.Template = EditorModalDesign.ButtonTemplate(typeof(Button));
        return button;
    }
    protected void CancelButton(string label = "취소")
    {
        // IsCancel provides the native Escape action; Close must run outside the pending request guard.
        var b = Action(label, Close);
        b.MinWidth = b.Width = 88; b.Template = EditorModalDesign.ButtonTemplate(typeof(Button));
    }
    protected static Border Card(UIElement content, Thickness padding) => new()
    { Background = EditorModalDesign.Color("#F9F9F9"), Padding = padding, CornerRadius = new(8), Child = content };

    protected FrameworkElement Entry(TextBox box, string label, string hint = "")
    {
        box.Template = (ControlTemplate)FindResource("LayoutEntry");
        var entry = Input(box, label, hint);
        if (entry is Grid wrapper && wrapper.Children.OfType<TextBlock>().SingleOrDefault() is { } placeholder)
        {
            placeholder.Margin = new(13, 0, 0, 0);
            placeholder.Foreground = EditorModalDesign.Color("#8A8A8A");
        }
        return entry;
    }

    protected Grid NumberField(TextBox box, string label, int minimum, int maximum)
    {
        void Step(int delta)
        {
            var next = int.TryParse(box.Text, out var n) ? Math.Clamp((long)n + delta, minimum, maximum) : minimum;
            box.SetCurrentValue(TextBox.TextProperty, next.ToString(CultureInfo.CurrentCulture));
        }
        return NumberField(box, label, Step);
    }

    protected Grid NumberField(TextBox box, string label, Action<int> step) =>
        FigmaDialogElements.NumberField(box, Entry(box, label), label, step);

    protected StackPanel Section(string label)
    {
        Fields.Children.Add(new TextBlock { Text = label, FontSize = 14, FontWeight = FontWeights.SemiBold,
            Height = 20, LineHeight = 20, LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            Foreground = EditorModalDesign.Color("#5F5F5F"), Margin = new(0, Fields.Children.Count == 0 ? 0 : 16, 0, 8) });
        var rows = new StackPanel();
        Fields.Children.Add(Card(rows, new(16, 12, 16, 12)));
        return rows;
    }

    protected static void FormRow(StackPanel panel, string label, FrameworkElement element, double labelWidth)
    {
        var row = new DockPanel { Margin = new(0, panel.Children.Count == 0 ? 0 : 12, 0, 0) };
        row.Children.Add(new TextBlock { Text = label, Width = labelWidth, FontSize = 14, LineHeight = 20, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(element); panel.Children.Add(row);
    }

    protected static FrameworkElement Input(Control field, string name, string hint = "")
    {
        AutomationProperties.SetName(field, name);
        field.Height = 32; field.MinHeight = 0; field.FontSize = 14; field.Padding = new(11, 0, 11, 0);
        field.VerticalContentAlignment = VerticalAlignment.Center; field.Background = Brushes.White;
        field.BorderBrush = EditorModalDesign.Color("#D1D1D1"); field.BorderThickness = new(1);
        if (field is PasswordBox)
        {
            var surface = new FrameworkElementFactory(typeof(Border), "Surface");
            surface.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            foreach (var property in new[] { Border.PaddingProperty, Border.BackgroundProperty, Border.BorderThicknessProperty, Border.BorderBrushProperty })
                surface.SetBinding(property, new System.Windows.Data.Binding(property.Name) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            var host = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
            host.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            surface.AppendChild(host);
            var template = new ControlTemplate(typeof(PasswordBox)) { VisualTree = surface };
            var focus = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
            focus.Setters.Add(new Setter(Border.BorderBrushProperty, EditorModalDesign.Color("#005FB8"), "Surface")); template.Triggers.Add(focus);
            field.Template = template;
        }
        if (hint.Length == 0) return field;
        var wrapper = new Grid();
        var watermark = new TextBlock { Text = hint, FontSize = 14, Foreground = EditorModalDesign.Color("#858585"),
            Margin = new(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false, Focusable = false };
        void Refresh(bool empty) => watermark.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        if (field is PasswordBox password)
        {
            void RefreshPassword() { using var value = password.SecurePassword; Refresh(value.Length == 0); }
            password.PasswordChanged += (_, _) => RefreshPassword(); RefreshPassword();
        }
        else if (field is TextBox text) { text.TextChanged += (_, _) => Refresh(text.Text.Length == 0); Refresh(text.Text.Length == 0); }
        wrapper.Children.Add(field); wrapper.Children.Add(watermark); return wrapper;
    }
}
