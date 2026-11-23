using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.ComponentModel;
using System.Windows.Media;
using Semojum.Core;

namespace Semojum.Desktop.Dialogs;

public sealed class SymbolDialog : FormDialogBase
{
    // The design's frequently used math symbols appear first. Cells always come from the engine.
    private const string CommonMath = "±×÷≠≤≥∞√∑∫∂∆∠⊥∥≒≡∈∉⊂⊃∪∩∴∵πθαβγ°′″";
    public BrailleSymbol? Selected { get; private set; }

    public SymbolDialog(BrailleSymbol[] symbols) : base("특수 기호 넣기", 600, 364,
        EditorModalDesign.Icon("form-close", assemblyName: "Semojum.UI.Common"))
    {
        AlignLabelBaseline();
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/Semojum.Feature.Editor.Wpf;component/Styles/SymbolViews.xaml", UriKind.Relative) });
        var filters = new WrapPanel();
        Fields.Children.Add(filters);
        var grid = new ListBox
        {
            BorderThickness = new(0), Padding = new(0), Background = Brushes.Transparent,
            ItemContainerStyle = (Style)FindResource("SymbolTile"), ItemsPanel = (ItemsPanelTemplate)FindResource("SymbolTiles"),
            DisplayMemberPath = nameof(BrailleSymbol.Char)
        };
        grid.GroupStyle.Add(new GroupStyle { Panel = (ItemsPanelTemplate)FindResource("SymbolSections"),
            ContainerStyle = (Style)FindResource("SymbolSection") });
        grid.Template = (ControlTemplate)FindResource("SymbolGrid");
        AutomationProperties.SetName(grid, "기호 격자");
        ScrollViewer.SetCanContentScroll(grid, false);
        ScrollViewer.SetHorizontalScrollBarVisibility(grid, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(grid, ScrollBarVisibility.Hidden);
        Fields.Children.Add(new Border { Margin = new(0,16,0,0), Padding = new(8,8,4,4), Height = 146,
            BorderThickness = new(1), BorderBrush = EditorModalDesign.Color("#EAEAEA"), Background = Brushes.White,
            CornerRadius = new(4), Child = grid });
        var glyph = new TextBlock { FontSize = 18 };
        var dots = new TextBlock { FontSize = 22, FontFamily = (FontFamily)FindResource("BraillePlainFont"), Margin = new(16,0,0,0) };
        var preview = new StackPanel { Orientation = Orientation.Horizontal, Height = 28, Margin = new(0,16,0,0) };
        preview.Children.Add(glyph);
        preview.Children.Add(dots);
        Fields.Children.Add(preview);
        var close = FooterButton("닫기", Close);
        close.IsCancel = true;
        close.Background = EditorModalDesign.Color("#FBFBFB");
        close.BorderBrush = EditorModalDesign.Color("#D1D1D1");
        var insert = FooterButton("넣기", Accept, true);
        grid.SelectionChanged += (_, _) =>
        {
            var symbol = grid.SelectedItem as BrailleSymbol;
            glyph.Text = symbol?.Char ?? "";
            dots.Text = symbol?.Cells ?? "";
            insert.IsEnabled = symbol is not null;
            AutomationProperties.SetName(preview, symbol is null ? "선택한 기호 없음" : $"{symbol.Char} · {symbol.Cells} · {symbol.Group}");
        };
        grid.MouseDoubleClick += (_, e) =>
        {
            if (ItemsControl.ContainerFromElement(grid, e.OriginalSource as DependencyObject) is ListBoxItem) Accept();
        };

        var buttons = new Dictionary<string, Button>();
        foreach (var name in new[] { "전체", "원", "괄호", "수학", "화살표", "단위" })
        {
            var label = new TextBlock { Text = name, FontSize = 12, Margin = new(11,0,11,0) };
            var button = new Button { Content = label, Height = 28, MinHeight = 0, MinWidth = 0, FontSize = 12,
                Padding = new(11,0,11,0), Margin = new(0,0,4,0), BorderThickness = new(1),
                Template = EditorModalDesign.ButtonTemplate(typeof(Button)) };
            AutomationProperties.SetName(button, name);
            button.Click += (_, _) => Filter(name);
            buttons.Add(name, button);
            filters.Children.Add(button);
        }
        // The full engine catalogue has additional named groups, available from All's context menu.
        var groups = new ContextMenu();
        foreach (var group in symbols.Select(s => s.Group).Distinct())
        {
            var item = new MenuItem { Header = group };
            item.Click += (_, _) => Filter("전체", group);
            groups.Items.Add(item);
        }
        buttons["전체"].ContextMenu = groups;
        buttons["전체"].ToolTip = "모든 기호 · 오른쪽 단추로 엔진 분류 선택";
        Filter("전체");

        void Filter(string category, string? engineGroup = null)
        {
            var previous = grid.SelectedItem as BrailleSymbol;
            var filtered = symbols.Where(s => engineGroup is null ? Includes(category, s) : s.Group == engineGroup);
            if (category == "수학") filtered = filtered.OrderByDescending(s => s.Group.Contains("수학")).DistinctBy(s => s.Char).OrderBy(s =>
            {
                var rank = s.Char.Length == 1 ? CommonMath.IndexOf(s.Char[0]) : -1;
                return rank < 0 ? int.MaxValue : rank;
            });
            var entries = filtered.ToArray();
            foreach (var (name, b) in buttons)
            {
                var active = name == category;
                b.Background = active ? EditorModalDesign.Color("#EFF6FC") : Brushes.Transparent;
                b.BorderBrush = active ? EditorModalDesign.Color("#005FB8") : Brushes.Transparent;
                b.Foreground = EditorModalDesign.Color(active ? "#003E92" : "#5F5F5F");
                AutomationProperties.SetItemStatus(b, active ? engineGroup ?? "선택됨" : "");
            }
            var view = new ListCollectionView(entries);
            if (category == "수학") view.GroupDescriptions.Add(new MathSection());
            grid.ItemsSource = view;
            grid.SelectedItem = previous is not null && entries.Contains(previous) ? previous : entries.FirstOrDefault();
            if (grid.SelectedItem is not null) grid.ScrollIntoView(grid.SelectedItem);
            insert.IsEnabled = grid.SelectedItem is BrailleSymbol;
            Status.Text = entries.Length == 0 ? "이 분류에 사용할 수 있는 기호가 없습니다." : engineGroup ?? "";
        }

        void Accept()
        {
            if (grid.SelectedItem is not BrailleSymbol symbol) return;
            Selected = symbol;
            DialogResult = true;
        }
    }

    // Keep the Figma palette together, then start a new row for additional engine symbols.
    private sealed class MathSection : GroupDescription
    {
        public override object GroupNameFromItem(object item, int level, CultureInfo culture) =>
            item is BrailleSymbol { Char.Length: 1 } symbol && CommonMath.Contains(symbol.Char[0]) ? 0 : 1;
    }

    private static bool Includes(string category, BrailleSymbol symbol)
    {
        var ch = symbol.Char;
        if (ch.Length == 0) return false;
        return category switch
        {
            "원" => ch.Length == 1 && ch[0] is (>= '①' and <= '⑳') or (>= 'Ⓐ' and <= '⓿') or (>= '❶' and <= '➓')
                or (>= '㉑' and <= '㉟') or (>= '㉠' and <= '㉻') or (>= '㊀' and <= '㊿') or (>= '㋐' and <= '㋾'),
            "괄호" => ch.Length == 1 && (char.GetUnicodeCategory(ch[0]) is UnicodeCategory.OpenPunctuation or UnicodeCategory.ClosePunctuation
                || ch[0] is (>= '⑴' and <= '⒇') or (>= '⒜' and <= '⒵') or (>= '㈀' and <= '㈞')),
            "수학" => symbol.Group.Contains("수학") || symbol.Group == "그리스문자" || ch.Length == 1 && CommonMath.Contains(ch[0]),
            "화살표" => symbol.Group == "화살표",
            "단위" => symbol.Group.Contains("단위"),
            _ => true
        };
    }
}

public sealed class SnippetDialog : FormDialogBase
{
    public AccountPreferences Result { get; private set; }

    public SnippetDialog(AccountPreferences current) : base("상용구 설정", 760, 574,
        EditorModalDesign.Icon("form-close", assemblyName: "Semojum.UI.Common"))
    {
        Result = current;
        AlignLabelBaseline();
        var columns = new Grid();
        columns.ColumnDefinitions.Add(new());
        columns.ColumnDefinitions.Add(new() { Width = new GridLength(16) });
        columns.ColumnDefinitions.Add(new());
        Fields.Children.Add(columns);
        var text = Column(0, "텍스트", "Ctrl", current.TextSnippets);
        var braille = Column(2, "점자", "Alt", current.BrailleSnippets);
        var cancel = FooterButton("취소", Close);
        cancel.IsCancel = true;
        cancel.Background = EditorModalDesign.Color("#FBFBFB");
        cancel.BorderBrush = EditorModalDesign.Color("#D1D1D1");
        FooterButton("확인", () =>
        {
            var updated = current with { TextSnippets = text.Select(box => box.Text).ToArray(), BrailleSnippets = braille.Select(box => box.Text).ToArray() };
            AccountPreferenceStore.Validate(updated);
            Result = updated;
            DialogResult = true;
        }, true);

        TextBox[] Column(int index, string name, string prefix, string[]? saved)
        {
            var column = new StackPanel();
            Grid.SetColumn(column, index);
            columns.Children.Add(column);
            column.Children.Add(new TextBlock { Text = $"{name} · {prefix}+0~9", FontSize = 14, FontWeight = FontWeights.SemiBold,
                Height = 20, Foreground = EditorModalDesign.Color("#5F5F5F") });
            var rows = new StackPanel();
            var card = Card(rows, new(12));
            card.Margin = new(0,8,0,0);
            column.Children.Add(card);
            var fields = new TextBox[10];
            for (var i = 0; i < fields.Length; i++)
            {
                var shortcut = $"{prefix}+{i}";
                var row = new DockPanel { Margin = new(0,i == 0 ? 0 : 8,0,0) };
                rows.Children.Add(row);
                var key = new TextBlock { Text = shortcut, Width = 52, FontSize = 12, Foreground = EditorModalDesign.Color("#5F5F5F") };
                DockPanel.SetDock(key, Dock.Left);
                row.Children.Add(key);
                var input = new TextBox { Text = saved?.ElementAtOrDefault(i) ?? "", MaxLength = 10000, AcceptsReturn = true,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden };
                fields[i] = input;
                row.Children.Add(Entry(input, shortcut, "비어 있음"));
                if (index == 2) input.FontFamily = (FontFamily)FindResource("BraillePlainFont");
            }
            return fields;
        }
    }
}
