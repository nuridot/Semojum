using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Semojum.Core;

namespace Semojum.Desktop.Dialogs;

public sealed class VisualDescriptionDialog : FormDialogBase
{
    private static readonly (string Key, string Label)[] Categories =
        [("figure", "그림"), ("table", "표"), ("chart", "차트"), ("diagram", "도표"), ("cartoon", "만화")];
    private readonly ListBox categories = new();
    private readonly StackPanel choices = new();
    private readonly List<Action> defaultIndicators = [];
    private readonly Dictionary<string, TextBlock> counts = [];
    private Dictionary<string, VisualDescription> values;
    public Dictionary<string, VisualDescription> Result { get; private set; }

    public VisualDescriptionDialog(Dictionary<string, VisualDescription> current)
        : base("대체 초안 유형", 680, 442, EditorModalDesign.Icon("form-close", assemblyName: "Semojum.UI.Common"))
    {
        Result = new(current);
        values = VisualDescriptions.Defaults();
        foreach (var (key, _) in Categories)
        {
            if (!current.TryGetValue(key, out var saved) || saved?.Options is not { } enabled) continue;
            var supported = VisualDescriptions.Choices[key].Where(enabled.Contains).ToArray();
            var preferred = supported.Contains(saved.DefaultOption) ? saved.DefaultOption : supported.FirstOrDefault() ?? "";
            values[key] = new(supported, preferred);
        }
        UseControlBorder();
        AlignLabelBaseline();
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/Semojum.Feature.Settings.Wpf;component/Styles/VisualDescription.xaml", UriKind.Relative) });

        var columns = new Grid();
        columns.ColumnDefinitions.Add(new() { Width = new(160) });
        columns.ColumnDefinitions.Add(new() { Width = new(16) });
        columns.ColumnDefinitions.Add(new());
        Fields.Children.Add(columns);
        categories.Background = Brushes.Transparent;
        categories.BorderThickness = new(0);
        categories.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        AutomationProperties.SetName(categories, "자료 유형");
        StripListPadding(categories);
        foreach (var (key, label) in Categories)
        {
            var line = new DockPanel();
            var count = counts[key] = new TextBlock { FontSize = 12, FontWeight = FontWeights.Normal, Foreground = EditorModalDesign.Color("#8A8A8A") };
            DockPanel.SetDock(count, Dock.Right);
            line.Children.Add(count);
            line.Children.Add(new TextBlock { Text = label, FontSize = 14 });
            var item = new ListBoxItem { Tag = key, Content = line, Height = 40, Padding = new(0), Margin = new(0, categories.Items.Count == 0 ? 0 : 4, 0, 0),
                Template = (ControlTemplate)FindResource("DraftCategory") };
            AutomationProperties.SetName(item, label);
            categories.Items.Add(item);
        }
        var nav = Card(categories, new(4,8,4,8));
        nav.VerticalAlignment = VerticalAlignment.Top;
        columns.Children.Add(nav);
        var right = new StackPanel();
        Grid.SetColumn(right, 2);
        columns.Children.Add(right);
        var head = new DockPanel { Height = 24, Margin = new(0,0,12,8) };
        var restore = Button("기본값", () =>
        {
            values = VisualDescriptions.Defaults();
            UpdateCounts();
            ShowChoices();
            Status.Text = "";
            return Task.CompletedTask;
        });
        var restoreLabel = new StackPanel { Orientation = Orientation.Horizontal };
        restoreLabel.Children.Add(EditorModalDesign.Icon("draft-types-sync",16,16,"Semojum.Feature.Settings.Wpf"));
        restoreLabel.Children.Add(new TextBlock { Text = "기본값", Margin = new(4,0,0,0), FontSize = 12, Foreground = EditorModalDesign.Color("#003E92") });
        restore.Content = restoreLabel;
        restore.MinWidth = 0; restore.MinHeight = 0; restore.Width = 70; restore.Height = 24; restore.Padding = new(8,0,8,0); restore.Margin = new(0);
        restore.Background = Brushes.Transparent; restore.BorderBrush = EditorModalDesign.Color("#EAEAEA"); restore.Template = EditorModalDesign.ButtonTemplate(typeof(Button));
        restore.ToolTip = "모든 자료 유형의 기본값 복원"; AutomationProperties.SetName(restore,"기본값 복원");
        DockPanel.SetDock(restore, Dock.Right); head.Children.Add(restore);
        head.Children.Add(new TextBlock { Text = "설명 유형", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = EditorModalDesign.Color("#5F5F5F") });
        right.Children.Add(head); right.Children.Add(choices);
        categories.SelectionChanged += (_, _) => ShowChoices();
        UpdateCounts();
        categories.SelectedIndex = 0;
        CancelButton();
        FooterButton("확인", () =>
        {
            var empty = Categories.FirstOrDefault(category => values[category.Key].Options.Length == 0);
            if (empty.Key is not null)
            {
                categories.SelectedIndex = Array.FindIndex(Categories, c => c.Key == empty.Key);
                throw new ArgumentException(empty.Label + "에 사용할 설명 유형을 하나 이상 골라 주세요.");
            }
            VisualDescriptions.Validate(values);
            Result = new(values);
            DialogResult = true;
        }, true);
    }

    private void UpdateCounts()
    {
        foreach (var (key, _) in Categories)
            counts[key].Text = $"{values[key].Options.Length}/{VisualDescriptions.Choices[key].Length}";
    }

    private void ShowChoices()
    {
        defaultIndicators.Clear();
        choices.Children.Clear();
        if (categories.SelectedIndex < 0) return;
        var (key, label) = Categories[categories.SelectedIndex];
        foreach (var option in VisualDescriptions.Choices[key])
        {
            var row = new Grid { Height = 40, Margin = new(0,choices.Children.Count == 0 ? 0 : 8,0,0) };
            var choice = new CheckBox { Content = option, FontSize = 14, IsChecked = values[key].Options.Contains(option),
                Margin = new(0), Template = (ControlTemplate)FindResource("DraftChoice") };
            AutomationProperties.SetName(choice, label + " " + option);
            row.Children.Add(choice);
            void SetPreferred()
            {
                values[key] = values[key] with { DefaultOption = option };
                RefreshDefaults();
            }
            var preferredCommand = new MenuItem { Header = "기본 설명으로 지정" };
            preferredCommand.Click += (_, _) => SetPreferred();
            choice.ContextMenu = new ContextMenu();
            choice.ContextMenu.Items.Add(preferredCommand);
            var makeDefault = Button("기본으로 지정", () => { SetPreferred(); return Task.CompletedTask; });
            makeDefault.Margin = new(0,0,8,0); makeDefault.MinHeight = 0; makeDefault.Height = 24;
            makeDefault.HorizontalAlignment = HorizontalAlignment.Right; makeDefault.VerticalAlignment = VerticalAlignment.Center;
            AutomationProperties.SetName(makeDefault, label + " " + option + " 기본 설명");
            row.Children.Add(makeDefault);
            // Show the secondary action while navigating the row by pointer or keyboard.
            void Refresh()
            {
                bool selected = values[key].DefaultOption == option;
                preferredCommand.IsEnabled = choice.IsChecked == true;
                makeDefault.Content = selected ? "기본 설명" : "기본으로 지정";
                makeDefault.Visibility = choice.IsChecked == true && (row.IsMouseOver || row.IsKeyboardFocusWithin) ? Visibility.Visible : Visibility.Collapsed;
                string hint = selected ? "현재 기본 설명" : "선택한 설명은 이 줄에서 기본 설명으로 지정할 수 있습니다.";
                choice.ToolTip = hint; AutomationProperties.SetHelpText(choice, hint);
            }
            void SelectOptions()
            {
                var selected = VisualDescriptions.Choices[key].Where(o => o == option ? choice.IsChecked == true : values[key].Options.Contains(o)).ToArray();
                var preferred = selected.Contains(values[key].DefaultOption) ? values[key].DefaultOption : selected.FirstOrDefault() ?? "";
                values[key] = new(selected, preferred);
                Status.Text = "";
                UpdateCounts();
                RefreshDefaults();
            }
            choice.Checked += (_, _) => SelectOptions();
            choice.Unchecked += (_, _) => SelectOptions();
            row.MouseEnter += (_, _) => Refresh();
            row.MouseLeave += (_, _) => Refresh();
            row.IsKeyboardFocusWithinChanged += (_, _) => Refresh();
            defaultIndicators.Add(Refresh);
            choices.Children.Add(row);
            Refresh();
        }
    }

    private void RefreshDefaults()
    {
        foreach (var refresh in defaultIndicators) refresh();
    }
}
