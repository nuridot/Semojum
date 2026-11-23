using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Semojum.Core;
using Semojum.Desktop.Services;

namespace Semojum.Desktop.Dialogs;

public sealed class AppearanceDialog : FormDialogBase
{
    private readonly Dictionary<string, TextBox> inputs = [];
    private readonly Dictionary<string, CheckBox> flags = [];
    private readonly ComboBox font = new() { IsEditable = true };
    public EditorAppearance Result { get; private set; }

    public AppearanceDialog(EditorAppearance current, Action<EditorAppearance> preview, bool colors = false, bool readingOptions = false)
        : base(readingOptions ? "점형 읽기 및 도구 모음" : "편집 화면 설정", 560, readingOptions ? 292 : colors ? 711 : 599,
            EditorModalDesign.Icon("form-close", assemblyName: "Semojum.UI.Common"))
    {
        Result = current;
        UseControlBorder();
        AlignLabelBaseline();
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/Semojum.UI.Common;component/Controls/SwitchTemplate.xaml", UriKind.Relative) });
        if (readingOptions)
        {
            var options = new StackPanel();
            AddSwitch(options, "SpeakDots", "커서 위치 점형 읽기", current.SpeakDots);
            AddNumber(options, "SpeechRate", "음성 속도", current.SpeechRate, -10, 10);
            AddSwitch(options, "ShowToolbar", "도구 모음 보기", current.ShowToolbar);
            Fields.Children.Add(Card(options, new(16, 12, 16, 12)));
            Fields.Children.Add(new TextBlock { Text = "Windows에 설치된 음성으로 현재 점형 번호를 읽습니다.", FontSize = 12, Margin = new(0, 8, 0, 0) });
        }
        else
        {
            Resources.MergedDictionaries.Add(new() { Source = new Uri("/Semojum.Feature.Shell.Wpf;component/Styles/AppearanceTabs.xaml", UriKind.Relative) });
            var tabs = new TabControl { Template = (ControlTemplate)FindResource("AppearanceTabs"), Padding = new(0), BorderThickness = new(0) };
            AddTab("문단 모양", Paragraph(current));
            AddTab("편집창 색상", Palette(current));
            tabs.SelectedIndex = colors ? 1 : 0;
            tabs.SelectionChanged += (_, e) =>
            {
                if (e.OriginalSource != tabs) return;
                Height = tabs.SelectedIndex == 1 ? 711 : 599;
                if (IsLoaded) WindowBounds.Fit(this);
                FormBody.ScrollToTop();
            };
            Fields.Children.Add(tabs);
            void AddTab(string title, StackPanel content)
            {
                tabs.Items.Add(new TabItem { Header = title, Template = (ControlTemplate)FindResource("AppearanceTab"),
                    Content = Card(content, new(16, 12, 16, 12)) });
            }
        }

        bool previewed = false;
        void Apply(EditorAppearance value)
        {
            AccountPreferenceStore.Validate(new(Appearance: value));
            preview(value);
            Result = value;
            previewed = true;
            Status.Text = "";
        }
        EditorAppearance ReadOptions() => current with { SpeakDots = On("SpeakDots"), SpeechRate = Integer("SpeechRate"), ShowToolbar = On("ShowToolbar") };
        void ApplyFields() => Apply(readingOptions ? ReadOptions() : Read(current));
        LeadingFooterButton("기본값", () =>
        {
            Apply(readingOptions ? current with { SpeakDots = false, SpeechRate = 0, ShowToolbar = true } : new());
            DialogResult = true;
            return Task.CompletedTask;
        });
        CancelButton();
        FooterButton("적용", ApplyFields);
        FooterButton("확인", () => { ApplyFields(); DialogResult = true; }, true);
        Closed += (_, _) => { if (previewed && DialogResult != true) preview(current); };
    }

    private StackPanel Paragraph(EditorAppearance value)
    {
        var rows = new StackPanel();
        font.ItemsSource = Fonts.SystemFontFamilies.Select(f => f.Source).Append("Pretendard").Append(value.FontFamily)
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).ToArray();
        font.Template = (ControlTemplate)FindResource("AppearanceFont");
        font.Text = value.FontFamily;
        FormRow(rows, "글꼴", SelectField(font, "글꼴", EditorModalDesign.Icon("appearance-chevron", 16, 16, "Semojum.Feature.Shell.Wpf")), 162);
        AddNumber(rows, "TextSize", "텍스트 크기", value.TextSize, 8, 72);
        AddNumber(rows, "BrailleSize", "점자 크기", value.BrailleSize, 8, 72);
        AddNumber(rows, "LineSpacing", "줄 간격", value.LineSpacing, 1, 3, 0.1m);
        AddNumber(rows, "CursorWidth", "커서 폭", value.CursorWidth, 1, 8);
        AddNumber(rows, "OutlineWidth", "판면 윤곽선 두께", value.OutlineWidth, 0, 8);
        AddNumber(rows, "RecentCount", "최근 파일 개수", value.RecentCount, 0, 50);
        AddSwitch(rows, "Bold", "진하게", value.Bold);
        AddSwitch(rows, "ShowLineNumbers", "줄 번호 보기", value.ShowLineNumbers);
        AddSwitch(rows, "ShowFormattingMarks", "공백 · 탭 · 줄바꿈 부호", value.ShowFormattingMarks);
        return rows;
    }

    private StackPanel Palette(EditorAppearance value)
    {
        var rows = new StackPanel();
        foreach (var (key, label, color) in new[] {
            ("TextColor", "글자", value.TextColor), ("BackgroundColor", "배경", value.BackgroundColor),
            ("SelectionTextColor", "선택 글자", value.SelectionTextColor), ("SelectionColor", "선택 배경", value.SelectionColor),
            ("BrailleColor", "점형 글자", value.BrailleColor), ("CurrentLineColor", "현재 줄 강조", value.CurrentLineColor),
            ("MarkerColor", "조판부호", value.MarkerColor), ("LineNumberColor", "줄 번호 글자", value.LineNumberColor),
            ("LineNumberBackground", "줄 번호 배경", value.LineNumberBackground), ("TagColor", "점역 코드", value.TagColor),
            ("OutlineColor", "판면 윤곽선", value.OutlineColor) })
        {
            var box = inputs[key] = new TextBox { Text = color, Width = 240, ToolTip = "#RRGGBB" };
            var swatch = new Border { Width = 24, Height = 24, CornerRadius = new(4), BorderThickness = new(1),
                BorderBrush = EditorModalDesign.Color("#D1D1D1"), Margin = new(0, 0, 12, 0) };
            void UpdateSwatch()
            {
                var hex = box.Text.Trim();
                swatch.Background = hex.Length == 7 && hex[0] == '#' && hex.Skip(1).All(Uri.IsHexDigit)
                    ? EditorModalDesign.Color(hex) : Brushes.Transparent;
            }
            box.TextChanged += (_, _) => UpdateSwatch();
            UpdateSwatch();
            var pair = new StackPanel { Orientation = Orientation.Horizontal };
            pair.Children.Add(swatch);
            pair.Children.Add(Entry(box, label));
            FormRow(rows, label, pair, 162);
        }
        AddSwitch(rows, "HighContrast", "고대비 테마", value.HighContrast);
        return rows;
    }

    private void AddNumber(StackPanel rows, string key, string label, double value, int minimum, int maximum, decimal increment = 1)
    {
        var box = inputs[key] = new TextBox { Text = value.ToString(CultureInfo.InvariantCulture) };
        var field = NumberField(box, label, direction =>
        {
            bool parsed = decimal.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n);
            var next = parsed ? Math.Clamp(Math.Clamp(n, minimum, maximum) + direction * increment, minimum, maximum) : minimum;
            box.SetCurrentValue(TextBox.TextProperty, next.ToString("G29", CultureInfo.InvariantCulture));
        });
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(field);
        row.Children.Add(new TextBlock { Text = $"({minimum}~{maximum})", FontSize = 12, Foreground = EditorModalDesign.Color("#5F5F5F"),
            Margin = new(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        FormRow(rows, label, row, 162);
    }

    private void AddSwitch(StackPanel rows, string key, string label, bool initial)
    {
        var toggle = flags[key] = new CheckBox { IsChecked = initial, Height = 20, FontSize = 14, Margin = new(0),
            HorizontalAlignment = HorizontalAlignment.Left, Template = (ControlTemplate)FindResource("SwitchTemplate") };
        AutomationProperties.SetName(toggle, label);
        FormRow(rows, label, toggle, 162);
    }

    private bool On(string key) => flags[key].IsChecked == true;
    private string TextValue(string key) => inputs[key].Text.Trim();
    private double Number(string key) => double.TryParse(TextValue(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
        ? value : throw new ArgumentException(AutomationProperties.GetName(inputs[key]) + "에 숫자를 입력해 주세요.");
    private int Integer(string key) => int.TryParse(TextValue(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
        ? value : throw new ArgumentException(AutomationProperties.GetName(inputs[key]) + "에 정수를 입력해 주세요.");

    private EditorAppearance Read(EditorAppearance initial) => initial with
    {
        FontFamily = font.Text.Trim(), TextSize = Number("TextSize"), BrailleSize = Number("BrailleSize"), LineSpacing = Number("LineSpacing"),
        CursorWidth = Number("CursorWidth"), OutlineWidth = Number("OutlineWidth"), RecentCount = Integer("RecentCount"),
        Bold = On("Bold"), ShowLineNumbers = On("ShowLineNumbers"), ShowFormattingMarks = On("ShowFormattingMarks"), HighContrast = On("HighContrast"),
        TextColor = TextValue("TextColor"), BackgroundColor = TextValue("BackgroundColor"), SelectionTextColor = TextValue("SelectionTextColor"),
        SelectionColor = TextValue("SelectionColor"), BrailleColor = TextValue("BrailleColor"), CurrentLineColor = TextValue("CurrentLineColor"),
        MarkerColor = TextValue("MarkerColor"), LineNumberColor = TextValue("LineNumberColor"), LineNumberBackground = TextValue("LineNumberBackground"),
        TagColor = TextValue("TagColor"), OutlineColor = TextValue("OutlineColor")
    };
}
