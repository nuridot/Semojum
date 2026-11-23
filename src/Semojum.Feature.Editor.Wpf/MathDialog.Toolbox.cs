using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using FormulaControl = Semojum.Desktop.Controls.FormulaPreview;

namespace Semojum.Desktop.Dialogs;

public sealed partial class MathDialog
{
    internal WrapPanel TemplateButtons { get; } = new();

    private FrameworkElement BuildToolbox()
    {
        var panel = new StackPanel();
        panel.Children.Add(SymbolButtons);
        var all = MathTemplates.Groups.SelectMany(group => group.Templates).ToArray();
        foreach (var (icon, label) in new[] {
            ("⅟", "분수"), ("√", "제곱근"), ("x²", "거듭제곱"), ("x₂", "아래 첨자"),
            ("∑", "합"), ("∏", "곱"), ("∫", "정적분"), ("lim", "극한"),
            ("( )", "소괄호"), ("[ ]", "대괄호"), ("{ }", "중괄호"), ("▦", "2×2 소괄호 행렬"),
            ("±", "플러스마이너스"), ("≤", "작거나 같음"), ("∈", "원소"), ("→", "오른쪽 화살표"),
            ("α", "알파"), ("sin", "사인"), ("∠", "각"), ("v⃗", "벡터"),
            ("⋯", "줄임표"), ("m²", "제곱미터") })
        {
            var template = all.Single(item => item.Label == label);
            var button = new Button
            {
                Content = new TextBlock { Text = icon, Margin = new(8, 0, 8, 0) }, MinWidth = 32, Height = 28, MinHeight = 0, Margin = new(0, 0, 4, 0),
                FontFamily = (FontFamily)FindResource("InterfaceFont"), FontSize = 12, Foreground = EditorModalDesign.Color("#003E92"),
                Template = EditorModalDesign.ButtonTemplate(typeof(Button)),
                Padding = new(8, 0, 8, 0), ToolTip = label, Background = Brushes.White, BorderBrush = EditorModalDesign.Color("#D1D1D1")
            };
            if (label == "벡터") button.FontFamily = new("Cambria Math");
            AutomationProperties.SetName(button, label);
            button.Click += (_, _) => { HighlightSymbol(button); InsertTemplate(template); };
            SymbolButtons.Children.Add(button);
        }
        TemplateButtons.Margin = new(0, 16, 0, 0);
        panel.Children.Add(TemplateButtons);
        foreach (var group in MathTemplates.Groups)
        {
            var content = new StackPanel();
            content.Children.Add(new TextBlock { Text = group.Icon, FontSize = 14, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
            var name = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, 2, 0, 0) };
            name.Children.Add(new TextBlock { Text = group.Label.Replace("·", " · "), FontSize = 12, Foreground = EditorModalDesign.Color("#5F5F5F") });
            var chevron = EditorModalDesign.Icon("math-template-chevron", 10, 10, "Semojum.Feature.Editor.Wpf");
            chevron.Margin = new(2, 0, 0, 0); name.Children.Add(chevron); content.Children.Add(name);
            content.Margin = new(4, 0, 4, 0);
            var button = new Button
            {
                Content = content, MinWidth = 56, Height = 56, MinHeight = 0, Padding = new(0), Margin = new(0, 0, 4, 0),
                Template = EditorModalDesign.ButtonTemplate(typeof(Button)),
                Background = Brushes.White, BorderBrush = EditorModalDesign.Color("#D1D1D1"), ToolTip = group.Label + " 수식 틀 선택"
            };
            AutomationProperties.SetName(button, group.Label + " 수식 틀");
            var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom, MaxHeight = 480 };
            foreach (var template in group.Templates)
            {
                var item = new MenuItem { Header = template.Label, Tag = template, ToolTip = template.Insert().Text };
                AutomationProperties.SetName(item, template.Label);
                item.Click += (_, _) => { HighlightSymbol(null); InsertTemplate(template); };
                menu.Items.Add(item);
            }
            var illustrated = false;
            menu.Opened += (_, _) =>
            {
                if (illustrated) return;
                illustrated = true;
                foreach (MenuItem item in menu.Items)
                {
                    var template = (MathTemplate)item.Tag;
                    var header = new Grid();
                    header.ColumnDefinitions.Add(new() { Width = new(130) });
                    header.ColumnDefinitions.Add(new() { Width = new(170) });
                    header.Children.Add(new TextBlock { Text = template.Label, VerticalAlignment = VerticalAlignment.Center });
                    try
                    {
                        var preview = new FormulaControl { Formula = template.Insert().Text, FontSize = 20, Margin = new(4) };
                        if (preview.HasError) continue;
                        var box = new Viewbox { Child = preview, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxHeight = 64 };
                        Grid.SetColumn(box, 1); header.Children.Add(box); item.Header = header;
                    }
                    catch { /* Keep the accessible text choice if a font cannot render the preview. */ }
                }
            };
            void OpenMenu() { menu.IsOpen = true; }
            button.ContextMenu = menu;
            button.Click += (_, _) => OpenMenu();
            button.PreviewKeyDown += (_, e) =>
            {
                if (e.Key != Key.Down) return;
                OpenMenu(); e.Handled = true;
            };
            TemplateButtons.Children.Add(button);
        }
        return panel;
    }

    private void HighlightSymbol(Button? selected)
    {
        foreach (Button button in SymbolButtons.Children)
        {
            button.Background = selected == button ? EditorModalDesign.Color("#EFF6FC") : Brushes.White;
            button.BorderBrush = EditorModalDesign.Color(selected == button ? "#005FB8" : "#D1D1D1");
        }
    }

    internal void InsertTemplate(MathTemplate template)
    {
        var start = Input.SelectionStart;
        var insertion = template.Insert(Input.SelectedText);
        Input.SelectedText = insertion.Text;
        Input.Focus();
        Input.Select(start + insertion.SelectionStart, insertion.SelectionLength);
    }
}
