using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;

namespace Semojum.Desktop.Dialogs;

public abstract class MemoPopover : CompactDialog
{
    protected MemoPopover(double width, double height) : base("메모", width, height, popover: true)
    {
        UseLayoutRounding = true;
        SizeToContent = SizeToContent.Height;
        var frame = (Border)Content;
        frame.CornerRadius = new(8);
        frame.BorderBrush = EditorModalDesign.Color("#EAEAEA");
        frame.Background = Background = EditorModalDesign.Color("#FBFBFB");
        var labels = new Style(typeof(TextBlock), (Style)FindResource(typeof(TextBlock)));
        labels.Setters.Add(new Setter(RenderTransformProperty, new TranslateTransform(0, -1)));
        Resources[typeof(TextBlock)] = labels;
    }
}

public sealed class MemoInputDialog : MemoPopover
{
    internal TextBox Input { get; } = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxLength = 2000,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalContentAlignment = VerticalAlignment.Top,
        FontSize = 14, MinHeight = 64, MaxHeight = 160, Padding = new(11, 7, 11, 7), Margin = new(0, 8, 0, 8) };
    public string Value => Input.Text.Trim();
    public MemoInputDialog(string? author = null) : base(320, 158)
    {
        Input.ToolTip = "Enter: 저장 · Shift+Enter: 줄 바꿈";
        MemoTextInputAppearance.Attach(this, Input);
        var panel = new StackPanel { Margin = new(12) }; Body(panel);
        panel.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(author) ? "작성자 미상" : author,
            FontSize = 14, FontWeight = FontWeights.SemiBold, Height = 20, LineHeight = 20,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = author });
        panel.Children.Add(Input);
        AutomationProperties.SetName(Input, "메모 내용");
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        panel.Children.Add(buttons);
        var cancel = Action("취소", () => DialogResult = false);
        var save = Action("저장", () => { if (Value.Length == 0) throw new ArgumentException("메모를 입력해 주세요."); DialogResult = true; }, true);
        foreach (var button in new[] { cancel, save })
        {
            Actions.Children.Remove(button);
            button.MinWidth = button.Width = 88; button.Height = 32;
            button.Margin = new(button == save ? 8 : 0, 0, 0, 0);
            button.Template = EditorModalDesign.ButtonTemplate(typeof(Button));
            buttons.Children.Add(button);
        }
        cancel.Background = EditorModalDesign.Color("#FBFBFB"); cancel.BorderBrush = EditorModalDesign.Color("#D1D1D1");
        InputBindings.Add(new KeyBinding(ApplicationCommands.Save, Key.Enter, ModifierKeys.Control) { CommandTarget = this });
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, (_, e) => { save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); e.Handled = true; }));
        Input.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return;
            save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); e.Handled = true;
        };
        Loaded += (_, _) => Input.Focus();
    }

    internal void ApplyInputAppearance(bool highContrast) => MemoTextInputAppearance.Apply(Input, highContrast);
}

internal static class MemoTextInputAppearance
{
    internal static void Attach(Window owner, TextBox input)
    {
        Apply(input, SystemParameters.HighContrast);
        System.ComponentModel.PropertyChangedEventHandler systemChanged = (_, e) =>
        {
            if (e.PropertyName == nameof(SystemParameters.HighContrast))
                owner.Dispatcher.BeginInvoke(new Action(() => Apply(input, SystemParameters.HighContrast)));
        };
        SystemParameters.StaticPropertyChanged += systemChanged;
        owner.Closed += (_, _) => SystemParameters.StaticPropertyChanged -= systemChanged;
    }

    internal static void Apply(TextBox input, bool highContrast) => input.Template = InputTemplate(highContrast);

    private static ControlTemplate InputTemplate(bool highContrast)
    {
        var surface = new FrameworkElementFactory(typeof(Border), "Surface");
        surface.SetBinding(Border.BackgroundProperty, new Binding(nameof(Control.Background)) { RelativeSource = RelativeSource.TemplatedParent });
        surface.SetValue(Border.BorderBrushProperty, EditorModalDesign.Color("#D1D1D1"));
        surface.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        surface.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        var host = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
        host.SetValue(UIElement.FocusableProperty, false);
        host.SetBinding(ScrollViewer.VerticalScrollBarVisibilityProperty, new Binding { Path = new PropertyPath("(0)", ScrollViewer.VerticalScrollBarVisibilityProperty), RelativeSource = RelativeSource.TemplatedParent });
        host.SetBinding(ScrollViewer.HorizontalScrollBarVisibilityProperty, new Binding { Path = new PropertyPath("(0)", ScrollViewer.HorizontalScrollBarVisibilityProperty), RelativeSource = RelativeSource.TemplatedParent });
        surface.AppendChild(host);
        var template = new ControlTemplate(typeof(TextBox)) { VisualTree = surface };
        foreach (var enabled in new[] { true, false })
        {
            var colors = new Trigger { Property = UIElement.IsEnabledProperty, Value = enabled };
            colors.Setters.Add(new Setter(Control.BackgroundProperty, highContrast ? new DynamicResourceExtension(SystemColors.WindowBrushKey) : Brushes.White));
            if (highContrast)
            {
                colors.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension(SystemColors.WindowTextBrushKey)));
                colors.Setters.Add(new Setter(TextBox.SelectionBrushProperty, new DynamicResourceExtension(SystemColors.HighlightBrushKey)));
            }
            template.Triggers.Add(colors);
        }
        var focus = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
        focus.Setters.Add(new Setter(Border.BorderBrushProperty, highContrast ? SystemColors.HighlightBrush : EditorModalDesign.Color("#005FB8"), "Surface"));
        template.Triggers.Add(focus);
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        if (highContrast) disabled.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension(SystemColors.GrayTextBrushKey)));
        else disabled.Setters.Add(new Setter(UIElement.OpacityProperty, .5));
        template.Triggers.Add(disabled);
        return template;
    }
}
