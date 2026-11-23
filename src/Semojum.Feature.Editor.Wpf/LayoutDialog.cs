using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Semojum.Core;

namespace Semojum.Desktop.Dialogs;

public sealed class LayoutDialog : FormDialogBase
{
    private const string AssetAssembly = "Semojum.Feature.Editor.Wpf";
    private readonly TextBox columns = new(), lines = new(), start = new(), sourceStart = new(), cover = new(), numberRanges = new();
    private readonly WrapPanel numbers, footerPages, align;
    private readonly CheckBox sourceNumber, brailleNumber, changeLine;
    public PageLayout Result { get; private set; }

    public LayoutDialog(PageLayout layout, Action<Window>? openOutputDefaults = null)
        : base("조판 설정", 560, 738, EditorModalDesign.Icon("table-cancel", assemblyName: AssetAssembly))
    {
        Result = layout;

        Resources.MergedDictionaries.Add(new() { Source = new Uri("/Semojum.UI.Common;component/Controls/CompactRadio.xaml", UriKind.Relative) });
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/Semojum.UI.Common;component/Controls/SwitchTemplate.xaml", UriKind.Relative) });

        var text = new Style(typeof(TextBlock), (Style)FindResource(typeof(TextBlock)));
        text.Setters.Add(new Setter(RenderTransformProperty, new TranslateTransform(0, -1)));
        Resources[typeof(TextBlock)] = text;

        var page = Section("판면");
        AddNumber(page, "페이지당 줄 수", lines, layout.Lines, 4, 100, "줄 (4~100)");
        AddNumber(page, "줄당 칸 수", columns, layout.Columns, 8, 100, "칸 (8~100)");
        AddNumber(page, "점자 페이지 시작 번호", start, layout.StartPage, 1, int.MaxValue);
        AddNumber(page, "원본 쪽 시작 번호", sourceStart, layout.SourcePageStart, 1, int.MaxValue);
        AddNumber(page, "표지 쪽 수", cover, layout.CoverPages, 0, int.MaxValue, "쪽");

        var footer = Section("번호 · 꼬리말");
        (string, string)[] sides = [("홀수", "홀수 페이지"), ("짝수", "짝수 페이지"), ("모든", "모든 페이지"), ("출력 안 함", "출력 안 함")];
        numbers = AddChoices(footer, "쪽 번호 넣을 면", sides, layout.PageNumbers);
        footerPages = AddChoices(footer, "꼬리말 넣을 면", sides, layout.Footer);
        align = AddChoices(footer, "꼬리말 정렬", [("가운데", "center"), ("오른쪽", "right"), ("왼쪽", "left")], layout.FooterAlign);
        numberRanges.Text = PageNumberRange.Format(layout.NumberRanges);
        FormRow(footer, "번호 범위", Entry(numberRanges, "번호 범위", "1-5, 8, 10-"), 162);

        var display = Section("표시");
        sourceNumber = AddSwitch(display, "원본 쪽 번호", layout.ShowSourcePageNumber);
        brailleNumber = AddSwitch(display, "점자 면 번호", layout.ShowBraillePageNumber);
        changeLine = AddSwitch(display, "묵자 쪽 전환선", layout.ShowChangeLine);

        if (openOutputDefaults is not null)
        {
            var defaults = new MenuItem { Header = "계정 출력 기본값" };
            defaults.Click += (_, _) => openOutputDefaults(this);
            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(defaults);
        }
        CancelButton();
        var cancel = Actions.Children.OfType<Button>().Single();
        cancel.Background = EditorModalDesign.Color("#FBFBFB");
        cancel.BorderBrush = EditorModalDesign.Color("#D1D1D1");
        FooterButton("확인", () => { Apply(); DialogResult = true; return Task.CompletedTask; }, true);
    }





    private void AddNumber(StackPanel group, string label, TextBox box, int value, int minimum, int maximum, string? unit = null)
    {
        box.Text = value.ToString(CultureInfo.CurrentCulture);
        var number = NumberField(box, label, minimum, maximum);
        var input = new StackPanel { Orientation = Orientation.Horizontal };
        input.Children.Add(number);
        if (unit is not null) input.Children.Add(new TextBlock { Text = unit, FontSize = 12,
            Foreground = EditorModalDesign.Color("#5F5F5F"), VerticalAlignment = VerticalAlignment.Center, Margin = new(8, 0, 0, 0) });
        FormRow(group, label, input, 162);
    }

    private static WrapPanel AddChoices(StackPanel group, string label, (string Text, string Value)[] choices, string current)
    {
        var panel = new WrapPanel();
        AutomationProperties.SetName(panel, label);
        foreach (var (text, value) in choices)
        {
            var caption = new TextBlock { Text = text };
            var radio = new RadioButton { Content = caption, Tag = value, Height = 20, IsChecked = value == current,
                MinWidth = text.Length switch { 2 => 49, 3 => 61, _ => 0 },
                Margin = new(0, 0, value == choices[^1].Value ? 0 : 16, 0) };
            AutomationProperties.SetName(radio, label + " " + text);
            panel.Children.Add(radio);
        }
        FormRow(group, label, panel, 162);
        return panel;
    }

    private CheckBox AddSwitch(StackPanel group, string label, bool current)
    {
        var toggle = new CheckBox { FontSize = 14, Margin = new(0), Padding = new(0), IsChecked = current,
            HorizontalAlignment = HorizontalAlignment.Left, Height = 20, Template = (ControlTemplate)FindResource("SwitchTemplate") };
        AutomationProperties.SetName(toggle, label);
        FormRow(group, label, toggle, 162);
        return toggle;
    }

    private void Apply()
    {
        static int Number(TextBox field) => int.TryParse(field.Text, out var value) ? value
            : throw new ArgumentException(AutomationProperties.GetName(field) + "에 숫자를 입력해 주세요.");
        static string Selection(Panel group, string original) =>
            group.Children.OfType<RadioButton>().FirstOrDefault(r => r.IsChecked == true)?.Tag as string ?? original;
        var value = Result with
        {
            Columns = Number(columns), Lines = Number(lines), StartPage = Number(start),
            SourcePageStart = Number(sourceStart), CoverPages = Number(cover), NumberRanges = PageNumberRange.Parse(numberRanges.Text),
            PageNumbers = Selection(numbers, Result.PageNumbers), Footer = Selection(footerPages, Result.Footer),
            FooterAlign = Selection(align, Result.FooterAlign), ShowSourcePageNumber = sourceNumber.IsChecked == true,
            ShowBraillePageNumber = brailleNumber.IsChecked == true, ShowChangeLine = changeLine.IsChecked == true
        };
        value.Validate();
        Result = value;
    }
}
