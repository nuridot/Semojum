using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SharpVectors.Converters;
using Semojum.Desktop.Dialogs;
using FormulaControl = Semojum.Desktop.Controls.FormulaPreview;
using WpfMath.Parsers;

namespace Semojum.Desktop.Controls;

/// <summary>Read-only rendering of the selected formula; editing remains in the formula dialog.</summary>
public sealed class MathPreviewDock : Border
{
    private readonly FormulaControl formula = new() { Scale = 28, FractionSlash = "/", FontWeight = FontWeights.SemiBold };
    private readonly TextBlock error = new() { Text = "수식을 표시할 수 없습니다. LaTeX 원문을 확인해 주세요.", Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBox latex = new() { IsReadOnly = true, Height = 32, MinHeight = 32, Padding = new(11, 0, 11, 0), VerticalContentAlignment = VerticalAlignment.Center, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden };
    private readonly Button edit = new() { Content = "편집", Width = 88, Height = 32, MinWidth = 0, MinHeight = 0, Padding = new(0), ToolTip = "수식 편집기로 열기" };
    public event Action? EditRequested;
    public event Action? CloseRequested;
    public MathPreviewDock()
    {
        Height = 102; MaxWidth = 760; CornerRadius = new(8); UseLayoutRounding = true;
        Background = Brushes.White; BorderBrush = new SolidColorBrush(Color.FromRgb(209, 209, 209)); BorderThickness = new(1);
        var root = new Grid(); root.RowDefinitions.Add(new() { Height = new(32) }); root.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) }); Child = root;
        var heading = new Border { Background = new SolidColorBrush(Color.FromRgb(249, 249, 249)), BorderBrush = new SolidColorBrush(Color.FromRgb(234, 234, 234)), BorderThickness = new(0, 0, 0, 1) };
        root.Children.Add(heading);
        var header = new DockPanel { Margin = new(12, 0, 4, 0) }; heading.Child = header;
        var close = new Button { Width = 28, Height = 28, Margin = new(4, 0, 0, 0), MinWidth = 0, MinHeight = 0, Padding = new(0), ToolTip = "수식 미리보기 닫기", Style = (Style)FindResource("PanelButton") };
        close.Content = new SvgViewbox { Source = new Uri("pack://application:,,,/Semojum.Feature.Editor.Wpf;component/Assets/Figma/math-preview-close-final.svg"), Width = 16, Height = 16, Stretch = Stretch.None, IsHitTestVisible = false };
        System.Windows.Automation.AutomationProperties.SetName(close, "수식 미리보기 닫기"); close.Click += (_, _) => CloseRequested?.Invoke();
        DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
        close.Template = EditorModalDesign.ButtonTemplate(typeof(Button)); close.Background = Brushes.Transparent; close.BorderThickness = new(0);
        edit.Template = EditorModalDesign.ButtonTemplate(typeof(Button)); edit.Background = EditorModalDesign.Color("#FBFBFB"); edit.BorderBrush = EditorModalDesign.Color("#D1D1D1"); edit.BorderThickness = new(1); edit.FontSize = 12; System.Windows.Automation.AutomationProperties.SetName(edit, "수식 편집기로 열기"); edit.Click += (_, _) => EditRequested?.Invoke();
        DockPanel.SetDock(edit, Dock.Right); header.Children.Add(edit);
        header.Children.Add(new TextBlock { Text = "수식 · LaTeX", FontFamily = (FontFamily)FindResource("InterfaceFont"), FontSize = 14, Foreground = EditorModalDesign.Color("#5F5F5F"), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        var body = new Grid { Margin = new(16, 0, 16, 0) };
        body.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        body.ColumnDefinitions.Add(new() { Width = new(24) });
        body.ColumnDefinitions.Add(new());
        var visual = new Grid { Height = 36, VerticalAlignment = VerticalAlignment.Center };
        visual.Children.Add(new Viewbox { Child = formula, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left });
        error.VerticalAlignment = VerticalAlignment.Center; error.FontSize = 12;
        error.SetBinding(ToolTipProperty, new System.Windows.Data.Binding(nameof(TextBlock.Text)) { Source = error });
        visual.Children.Add(error); body.Children.Add(visual);
        // Reserve room for selectable LaTeX when a fraction, matrix or error is very wide.
        body.SizeChanged += (_, e) => visual.MaxWidth = Math.Max(1, (e.NewSize.Width - 24) * .55);
        EditorModalDesign.Input(latex);
        latex.SetResourceReference(Control.FontFamilyProperty, "MonoFont"); latex.Background = EditorModalDesign.Color("#F9F9F9");
        latex.Padding = new(6, 0, 6, 0); latex.BorderThickness = new(0);
        System.Windows.Automation.AutomationProperties.SetName(latex, "선택한 수식 LaTeX 원문");
        Grid.SetColumn(latex, 2); body.Children.Add(latex);
        Grid.SetRow(body, 1); root.Children.Add(body);
    }
    public void ShowFormula(string value, bool canEdit)
    {
        latex.Text = value; edit.IsEnabled = canEdit; error.Text = "수식을 표시할 수 없습니다. LaTeX 원문을 확인해 주세요.";
        try
        {
            WpfTeXFormulaParser.Instance.Parse(value);
            // Rendering errors are reported through HasError, not necessarily thrown by the parser.
            // Clear first so a failed or repeated expression cannot retain the previous drawing.
            formula.Formula = ""; formula.Formula = value;
            if (formula.HasError) throw new InvalidOperationException("Formula rendering failed.");
            formula.Visibility = Visibility.Visible; error.Visibility = Visibility.Collapsed;
        }
        catch (Exception)
        {
            formula.Formula = ""; formula.Visibility = Visibility.Collapsed; error.Visibility = Visibility.Visible;
        }
    }
    public void ShowInvalidMarkup()
    {
        latex.Text = ""; edit.IsEnabled = false; formula.Formula = ""; formula.Visibility = Visibility.Collapsed;
        error.Text = "수식 태그가 중첩되었거나 닫히지 않았습니다. 원문을 확인해 주세요.";
        error.Visibility = Visibility.Visible;
    }
}
