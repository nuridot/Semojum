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
    private readonly VisualOptionCatalog catalog;
    private readonly bool useNumbers;
    private readonly Dictionary<string, HashSet<int>> enabled = [];
    private readonly Dictionary<string, int> preferred = [];
    public Dictionary<string, VisualDescription> Result { get; private set; }

    public VisualDescriptionDialog(Dictionary<string, VisualDescription> current, VisualOptionCatalog? catalog = null)
        : base("대체 초안 유형", 680, 442, EditorModalDesign.Icon("form-close", assemblyName: "Semojum.UI.Common"))
    {
        Result = new(current);
        this.catalog = catalog ?? VisualOptionCatalog.Legacy();
        useNumbers = catalog != null;
        LoadSelections(current);
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
            LoadSelections(VisualDescriptions.Defaults(), restoringDefaults: true);
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
            var empty = Categories.FirstOrDefault(category => enabled[category.Key].Count == 0);
            if (empty.Key is not null)
            {
                categories.SelectedIndex = Array.FindIndex(Categories, c => c.Key == empty.Key);
                throw new ArgumentException(empty.Label + "에 사용할 설명 유형을 하나 이상 골라 주세요.");
            }
            var result = new Dictionary<string, VisualDescription>();
            foreach (var (key, _) in Categories)
            {
                var selected = this.catalog.Choices[key].Where(item => enabled[key].Contains(item.Option)).ToArray();
                var choice = selected.First(item => item.Option == preferred[key]);
                // Keep historical labels readable by earlier clients; the live catalog supplies the displayed text.
                string SavedLabel(VisualChoice item) => VisualDescriptions.Choices[key]
                    .FirstOrDefault(label => VisualDescriptions.LegacyNumber(label, key == "table") == item.Option) ?? item.Label;
                result[key] = new(selected.Select(SavedLabel).ToArray(), SavedLabel(choice),
                    useNumbers ? selected.Select(item => item.Option).ToArray() : null, useNumbers ? choice.Option : null);
            }
            VisualDescriptions.Validate(result);
            Result = result;
            DialogResult = true;
        }, true);
    }

    private void LoadSelections(IReadOnlyDictionary<string, VisualDescription> current, bool restoringDefaults = false)
    {
        var defaults = VisualDescriptions.Defaults();
        foreach (var (key, _) in Categories)
        {
            var saved = current.TryGetValue(key, out var policy) && policy?.Options != null ? policy : defaults[key];
            var offered = catalog.Choices[key];
            var numbers = saved.OptionNumbers?.ToHashSet() ?? saved.Options
                .Select(label => VisualDescriptions.LegacyNumber(label, key == "table")).OfType<int>().ToHashSet();
            enabled[key] = offered.Where(row => numbers.Contains(row.Option)).Select(row => row.Option).ToHashSet();
            if (enabled[key].Count == 0 && (restoringDefaults || !current.ContainsKey(key))) enabled[key].Add(offered[0].Option);
            int defaultNumber = saved.DefaultOptionNumber ?? VisualDescriptions.LegacyNumber(saved.DefaultOption, key == "table") ?? 0;
            preferred[key] = enabled[key].Contains(defaultNumber) ? defaultNumber : offered.FirstOrDefault(row => enabled[key].Contains(row.Option))?.Option ?? 0;
        }
    }

    private void UpdateCounts()
    {
        foreach (var (key, _) in Categories)
            counts[key].Text = $"{enabled[key].Count}/{catalog.Choices[key].Length}";
    }

    private void ShowChoices()
    {
        defaultIndicators.Clear();
        choices.Children.Clear();
        if (categories.SelectedIndex < 0) return;
        var (key, label) = Categories[categories.SelectedIndex];
        foreach (var option in catalog.Choices[key])
        {
            var row = new Grid { Height = 40, Margin = new(0,choices.Children.Count == 0 ? 0 : 8,0,0) };
            var choice = new CheckBox { Content = option.Label, FontSize = 14, IsChecked = enabled[key].Contains(option.Option),
                Margin = new(0), Template = (ControlTemplate)FindResource("DraftChoice") };
            AutomationProperties.SetName(choice, label + " " + option.Label);
            row.Children.Add(choice);
            void SetPreferred()
            {
                preferred[key] = option.Option;
                RefreshDefaults();
            }
            var preferredCommand = new MenuItem { Header = "기본 설명으로 지정" };
            preferredCommand.Click += (_, _) => SetPreferred();
            choice.ContextMenu = new ContextMenu();
            choice.ContextMenu.Items.Add(preferredCommand);
            var makeDefault = Button("기본으로 지정", () => { SetPreferred(); return Task.CompletedTask; });
            makeDefault.Margin = new(0,0,8,0); makeDefault.MinHeight = 0; makeDefault.Height = 24;
            makeDefault.HorizontalAlignment = HorizontalAlignment.Right; makeDefault.VerticalAlignment = VerticalAlignment.Center;
            AutomationProperties.SetName(makeDefault, label + " " + option.Label + " 기본 설명");
            row.Children.Add(makeDefault);
            // Show the secondary action while navigating the row by pointer or keyboard.
            void Refresh()
            {
                bool selected = preferred[key] == option.Option;
                preferredCommand.IsEnabled = choice.IsChecked == true;
                makeDefault.Content = selected ? "기본 설명" : "기본으로 지정";
                makeDefault.Visibility = choice.IsChecked == true && (row.IsMouseOver || row.IsKeyboardFocusWithin) ? Visibility.Visible : Visibility.Collapsed;
                string hint = selected ? "현재 기본 설명" : "선택한 설명은 이 줄에서 기본 설명으로 지정할 수 있습니다.";
                choice.ToolTip = hint; AutomationProperties.SetHelpText(choice, hint);
            }
            void SelectOptions()
            {
                if (choice.IsChecked == true) enabled[key].Add(option.Option);
                else enabled[key].Remove(option.Option);
                if (!enabled[key].Contains(preferred[key]))
                    preferred[key] = catalog.Choices[key].FirstOrDefault(item => enabled[key].Contains(item.Option))?.Option ?? 0;
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
