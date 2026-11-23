using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Semojum.Core;
using Semojum.Core.Integration;
using Semojum.Desktop.Controls;
using static Semojum.Desktop.Dialogs.FigmaDialogElements;

namespace Semojum.Desktop.Dialogs;

public sealed class OptionsEditor : StackPanel
{
    private readonly SegmentedChoice korean, rule, mixed, spaces, wrap, choices;
    private readonly bool grouped;
    private StackPanel? currentGroup;
    private bool englishGrade1;
    private Dictionary<string, VisualDescription> visual = [];
    public OptionsEditor(bool grouped = false)
    {
        this.grouped = grouped;
        if (grouped) BeginGroup("글과 규정");
        korean = Pair("한글", "정자(1급)", "약자(2급)");
        rule = Pair("점역 규정", "한글 규정", "영어 규정"); mixed = Pair("한영 혼용 규정", "한국어", "영어");
        if (grouped) BeginGroup("줄과 빈칸");
        spaces = Pair("연속 공백", "그대로", "하나로"); wrap = Pair("줄바꿈 단위", "음절", "어절");
        choices = Pair("선택지 배치", "한 줄에 하나씩", "짧으면 합쳐 적기");
        rule.ToolTip = mixed.ToolTip = spaces.ToolTip = "이 설정은 저장되며 서버 변환에 사용됩니다. 현재 로컬 재점역에는 적용되지 않습니다.";
        Set(new());
    }
    private void BeginGroup(string title)
    {
        Children.Add(new TextBlock { Text = title, FontSize = 12, Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0, 16, 0, 4) });
        currentGroup = new StackPanel(); var card = EditorModalDesign.Card(currentGroup, new Thickness(0)); card.Padding = new Thickness(16, 12, 16, 8); Children.Add(card);
    }
    private SegmentedChoice Pair(string label, params string[] labels)
    {
        var box = new SegmentedChoice(labels);
        if (grouped) { FooterDesign.StyleAlignment(box); box.Width = 264; AutomationProperties.SetName(box, label); FooterDesign.Row(currentGroup!, label, box); }
        else CompactDialog.SettingRow(this, label, box);
        return box;
    }
    public void Set(BrailleOptions value)
    {
        korean.SelectedIndex = value.KoreanGrade1 ? 0 : 1; englishGrade1 = value.EnglishGrade1;
        rule.SelectedIndex = value.EnglishDocument ? 1 : 0; mixed.SelectedIndex = value.MixedRule == "english" ? 1 : 0;
        spaces.SelectedIndex = value.ConsecutiveSpace == "keep" ? 0 : 1; wrap.SelectedIndex = value.LineBreakUnit == "word" ? 1 : 0;
        choices.SelectedIndex = value.ChoiceLayout == "onePerLine" ? 0 : 1;
        visual = new(value.VisualDescriptions ?? VisualDescriptions.Defaults());
    }
    public BrailleOptions Value => new(korean.SelectedIndex == 0, englishGrade1, rule.SelectedIndex == 1,
        wrap.SelectedIndex == 1 ? "word" : "syllable", mixed.SelectedIndex == 1 ? "english" : "korean",
        spaces.SelectedIndex == 0 ? "keep" : "single", choices.SelectedIndex == 0 ? "onePerLine" : "merge", new(visual));
}

public sealed class DefaultsDialog : ConversionFormDialog
{
    public DefaultsDialog(SettingsClient client) : base("기본 점역 옵션", 560, 670)
    {
        var translation = Section("점역");
        var forms = new ComboBox { DisplayMemberPath = nameof(BrailleForm.Name) };
        FormRow(translation, "점역 양식", SelectField(forms, "점역 양식"), 162);
        Pair AddPair(StackPanel group, string label, string a, string b)
        {
            var pair = new Pair(label, a, b);
            FormRow(group, label, pair, 162); return pair;
        }
        var korean = AddPair(translation, "한글", "정자(1급)", "약자(2급)");
        var rule = AddPair(translation, "점역 규정", "한글 규정", "영어 규정");
        var english = AddPair(translation, "영문", "정자(1급)", "약자(2급)");
        var mixed = AddPair(translation, "한영 혼용 규정", "한국어", "영어");
        var layout = Section("조판");
        var spaces = AddPair(layout, "연속 공백", "그대로", "하나로");
        var wrap = AddPair(layout, "줄바꿈 단위", "음절", "어절");
        var choice = AddPair(layout, "선택지 배치", "한 줄에 하나씩", "짧으면 합쳐 적기");
        var pageLine = AddPair(layout, "페이지 번호 표시줄 면", "홀수", "모든");
        var output = Section("내보낼 꼴");
        foreach (var (label, value) in new[] { ("점역문서 형식", ".brf 점자 파일"), ("교열문서 형식", ".txt 텍스트 파일") })
            FormRow(output, label, SelectField(Choices([value], value), label), 162);

        BrailleOptions current = new();
        string savedPageLine = "odd";
        void SetOptions(BrailleOptions options)
        {
            current = options; korean.FirstSelected = options.KoreanGrade1; english.FirstSelected = options.EnglishGrade1;
            rule.FirstSelected = !options.EnglishDocument; mixed.FirstSelected = options.MixedRule != "english";
            spaces.FirstSelected = options.ConsecutiveSpace == "keep"; wrap.FirstSelected = options.LineBreakUnit != "word";
            choice.FirstSelected = options.ChoiceLayout == "onePerLine";
        }
        forms.SelectionChanged += (_, _) => { if (forms.SelectedItem is BrailleForm f) SetOptions(f.Options); };
        var restore = FooterButton("기본값 복원", () =>
        {
            forms.SelectedItem = forms.Items.Cast<BrailleForm>().FirstOrDefault(f => f.FormId == "builtin:GENERAL_BOOK");
            SetOptions(new()); pageLine.FirstSelected = true;
        });
        var footer = (Border)Actions.Parent;
        Actions.Children.Remove(restore); footer.Child = null;
        var buttons = new DockPanel();
        restore.Margin = new(24, 16, 0, 16); buttons.Children.Add(restore); buttons.Children.Add(Actions); footer.Child = buttons;

        CancelButton();
        var save = FooterButton("확인", async () =>
        {
            var options = current with { KoreanGrade1 = korean.FirstSelected, EnglishGrade1 = english.FirstSelected,
                EnglishDocument = !rule.FirstSelected, MixedRule = mixed.FirstSelected ? "korean" : "english",
                ConsecutiveSpace = spaces.FirstSelected ? "keep" : "single", LineBreakUnit = wrap.FirstSelected ? "syllable" : "word",
                ChoiceLayout = choice.FirstSelected ? "onePerLine" : "merge" };
            var pageNumberLine = pageLine.FirstSelected ? "odd" : pageLine.Second.IsChecked == true ? "every" : savedPageLine;
            await client.SaveDefaultsAsync(new(options, (forms.SelectedItem as BrailleForm)?.FormId, pageNumberLine), Lifetime.Token);
            DialogResult = true;
        }, true);
        restore.IsEnabled = save.IsEnabled = false;
        OnLoad(async () =>
        {
            var all = await client.FormsAsync(Lifetime.Token); var defaults = await client.DefaultsAsync(Lifetime.Token);
            forms.ItemsSource = all; forms.SelectedItem = all.FirstOrDefault(f => f.FormId == defaults.FormId);
            SetOptions(defaults.Options); savedPageLine = defaults.PageNumberLine;
            pageLine.First.IsChecked = savedPageLine == "odd"; pageLine.Second.IsChecked = savedPageLine == "every";
            restore.IsEnabled = save.IsEnabled = true;
        });
    }
}

public sealed class FormDialog : ConversionFormDialog
{
    public BrailleForm? Selected { get; private set; }

    public FormDialog(SettingsClient client, PageLayout currentLayout, BrailleOptions currentOptions, string? selectedId = null)
        : base("점역 양식", 560, 326)
    {
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/Semojum.Feature.Settings.Wpf;component/FormsPalette.xaml", UriKind.Relative) });
        var body = new Grid();
        body.ColumnDefinitions.Add(new() { Width = new(220) });
        body.ColumnDefinitions.Add(new() { Width = new(16) });
        body.ColumnDefinitions.Add(new());
        Fields.Children.Add(body);

        var picker = new ListBox { Background = Brushes.Transparent, BorderThickness = new(0), Padding = new(0),
            ItemTemplate = (DataTemplate)FindResource("BrailleFormRow"), ItemContainerStyle = (Style)FindResource("BrailleFormItem") };
        AutomationProperties.SetName(picker, "양식 목록");
        StripListPadding(picker);
        body.Children.Add(new Border { Child = picker, VerticalAlignment = VerticalAlignment.Top, Height = 190,
            BorderThickness = new(1), CornerRadius = new(4), BorderBrush = Brush("#EAEAEA"), Padding = new(0,4,0,4), Background = Brushes.White });

        var rows = new StackPanel();
        var card = Card(rows, new(16,12,16,12));
        Grid.SetColumn(card, 2);
        body.Children.Add(card);
        string[] labels = ["줄 · 칸", "한글 급수", "줄바꿈 단위", "쪽 번호 면", "꼬리말"];
        var values = new List<TextBlock>();
        foreach (var label in labels)
        {
            var row = new DockPanel { Height = 28, Margin = new(0,rows.Children.Count == 0 ? 0 : 8,0,0) };
            row.Children.Add(new TextBlock { Text = label, FontSize = 14, Foreground = Brush("#5F5F5F"), Width = 108 });
            var value = new TextBlock { FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis };
            AutomationProperties.SetAutomationId(value, "FormSummary" + values.Count);
            values.Add(value);
            row.Children.Add(value);
            rows.Children.Add(row);
        }

        var settings = LeadingFooterButton("사용자 양식 설정", EditAsync, 113);
        CancelButton();
        var confirm = FooterButton("확인", () =>
        {
            if (picker.SelectedItem is not BrailleForm form) return;
            Selected = form;
            DialogResult = true;
        }, true);
        settings.IsEnabled = false;
        void ShowSummary()
        {
            var form = picker.SelectedItem as BrailleForm;
            var descriptions = form is null ? Enumerable.Repeat("—", labels.Length).ToArray() : new[]
            {
                $"{form.Layout.Lines}줄 × {form.Layout.Columns}칸",
                form.Options.KoreanGrade1 ? "정자(1급)" : "약자(2급)",
                form.Options.LineBreakUnit == "word" ? "어절" : "음절",
                Side(form.Layout.PageNumbers),
                string.IsNullOrWhiteSpace(form.Layout.Footer) || form.Layout.Footer == "출력 안 함" ? "출력 안 함"
                    : Side(form.Layout.Footer) + " · " + (form.Layout.FooterAlign switch { "right" => "오른쪽", "left" => "왼쪽", _ => "가운데" })
            };
            for (var i = 0; i < values.Count; i++)
            {
                values[i].Text = descriptions[i];
                values[i].ToolTip = descriptions[i];
                AutomationProperties.SetName(values[i], labels[i] + " " + descriptions[i]);
            }
            confirm.IsEnabled = form is not null;
        }
        picker.SelectionChanged += (_, _) => ShowSummary();
        ShowSummary();
        Loaded += async (_, _) => await RunAsync(async () =>
        {
            var forms = await client.FormsAsync(Lifetime.Token);
            if (Lifetime.IsCancellationRequested) return;
            ShowForms(forms, selectedId);
            settings.IsEnabled = true;
        });

        void ShowForms(BrailleForm[] forms, string? id)
        {
            picker.ItemsSource = forms;
            var selected = forms.FirstOrDefault(f => f.FormId == id);
            if (selected is null)
            {
                var layout = JsonSerializer.SerializeToNode(currentLayout);
                var options = JsonSerializer.SerializeToNode(currentOptions);
                selected = forms.FirstOrDefault(f => JsonNode.DeepEquals(JsonSerializer.SerializeToNode(f.Layout), layout)
                    && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(f.Options), options));
            }
            picker.SelectedItem = selected ?? forms.FirstOrDefault();
            if (picker.SelectedItem is not null) picker.ScrollIntoView(picker.SelectedItem);
        }
        Task EditAsync()
        {
            var form = picker.SelectedItem as BrailleForm;
            var forms = picker.Items.Cast<BrailleForm>().ToList();
            var dialog = new FormEditDialog(client, forms.ToArray(), form is { Builtin: false } ? form : null,
                form?.Layout ?? currentLayout, form?.Options ?? currentOptions, form?.FormId) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.Saved is { } saved)
            {
                var index = forms.FindIndex(f => f.FormId == saved.FormId);
                if (index < 0) forms.Add(saved);
                else forms[index] = saved;
                ShowForms(forms.ToArray(), saved.FormId);
            }
            else if (dialog.Deleted && form is not null)
                ShowForms(forms.Where(f => f.FormId != form.FormId).ToArray(), null);
            return Task.CompletedTask;
        }
    }

    private static string Side(string? value) => value switch
    {
        "홀수 페이지" => "홀수", "짝수 페이지" => "짝수", "모든 페이지" => "모든", null or "" => "—", _ => value
    };
}

public sealed class FormEditDialog : ConversionFormDialog
{
    public BrailleForm? Saved { get; private set; }
    public bool Deleted { get; private set; }

    public FormEditDialog(SettingsClient client, BrailleForm[] forms, BrailleForm? current, PageLayout layout, BrailleOptions options, string? baseId)
        : base("사용자 양식 설정", 560, 446)
    {
        var personal = current is { Builtin: false } ? current : null;
        var rows = new StackPanel();
        Fields.Children.Add(Card(rows, new(16,12,16,12)));
        var name = new TextBox { Text = personal?.Name ?? "", MaxLength = 100 };
        FormRow(rows, "양식 이름", Entry(name, "양식 이름"), 162);
        var basis = new ComboBox { ItemsSource = forms.Where(f => f.FormId != personal?.FormId).ToArray(), DisplayMemberPath = nameof(BrailleForm.Name) };
        var originalBaseId = personal is null ? current?.FormId ?? baseId : personal.BaseFormId;
        basis.SelectedItem = forms.FirstOrDefault(f => f.FormId == originalBaseId);
        FormRow(rows, "바탕 양식", SelectField(basis, "바탕 양식"), 162);
        var lines = Number("줄 수", "줄", 4);
        var columns = Number("칸 수", "칸", 8);
        var korean = new Pair("한글", "정자(1급)", "약자(2급)");
        var wrap = new Pair("줄바꿈 단위", "음절", "어절");
        FormRow(rows, "한글", korean, 162);
        FormRow(rows, "줄바꿈 단위", wrap, 162);
        var sides = Radios("쪽 번호 넣을 면", [("홀수","홀수 페이지"),("짝수","짝수 페이지"),("모든","모든 페이지"),("출력 안 함","출력 안 함")]);
        var alignment = Radios("꼬리말 정렬", [("가운데","center"),("오른쪽","right"),("왼쪽","left")]);
        ApplyFields();
        basis.SelectionChanged += (_, _) =>
        {
            if (basis.SelectedItem is not BrailleForm selected) return;
            layout = selected.Layout;
            options = selected.Options;
            ApplyFields();
        };
        var delete = LeadingFooterButton("지우기", async () =>
        {
            if (personal is null || new DeleteFormDialog(personal.Name) { Owner = this }.ShowDialog() != true) return;
            await client.DeleteFormAsync(personal, Lifetime.Token);
            Deleted = true;
            DialogResult = false;
        });
        delete.IsEnabled = personal is not null;
        delete.ToolTip = personal is null ? "기본 양식은 지울 수 없습니다." : "사용자 양식 지우기";
        ToolTipService.SetShowOnDisabled(delete, true);
        CancelButton();
        FooterButton("저장", async () =>
        {
            if (!int.TryParse(lines.Text, out var l) || !int.TryParse(columns.Text, out var c))
                throw new ArgumentException("줄 수와 칸 수를 정수로 입력해 주세요.");
            var updatedLayout = layout with { Lines = l, Columns = c,
                PageNumbers = sides.FirstOrDefault(r => r.IsChecked == true)?.Tag as string ?? layout.PageNumbers,
                FooterAlign = alignment.FirstOrDefault(r => r.IsChecked == true)?.Tag as string ?? layout.FooterAlign };
            var updatedOptions = options with { KoreanGrade1 = korean.FirstSelected, LineBreakUnit = wrap.FirstSelected ? "syllable" : "word" };
            Saved = await client.SaveFormAsync(personal?.FormId, name.Text, (basis.SelectedItem as BrailleForm)?.FormId ?? originalBaseId,
                updatedLayout, updatedOptions, Lifetime.Token);
            DialogResult = true;
        }, true);

        void ApplyFields()
        {
            lines.Text = layout.Lines.ToString();
            columns.Text = layout.Columns.ToString();
            korean.FirstSelected = options.KoreanGrade1;
            wrap.FirstSelected = options.LineBreakUnit != "word";
            foreach (var radio in sides) radio.IsChecked = Equals(radio.Tag, layout.PageNumbers);
            foreach (var radio in alignment) radio.IsChecked = Equals(radio.Tag, layout.FooterAlign);
        }
        TextBox Number(string label, string unit, int min)
        {
            var box = new TextBox();
            var group = new StackPanel { Orientation = Orientation.Horizontal };
            group.Children.Add(NumberField(box, label, min, 100));
            group.Children.Add(new TextBlock { Text = unit, FontSize = 12, Margin = new(8,0,0,0), Foreground = Brush("#5F5F5F") });
            FormRow(rows, label, group, 162);
            return box;
        }
        RadioButton[] Radios(string label, (string Name, string Value)[] choices)
        {
            var group = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var (caption, value) in choices)
            {
                var radio = new RadioButton { Content = new TextBlock { Text = caption }, Height = 20, Tag = value,
                    Margin = new(group.Children.Count == 0 ? 0 : 16,0,0,0) };
                AutomationProperties.SetName(radio, label + " " + caption);
                group.Children.Add(radio);
            }
            FormRow(rows, label, group, 162);
            return group.Children.Cast<RadioButton>().ToArray();
        }
    }
}
