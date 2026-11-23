using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Semojum.Core;

namespace Semojum.Desktop.Dialogs;

public sealed class MemoListDialog : FormDialogBase
{
    private sealed record Item(EditorBlock Block, LineMemo Memo)
    {
        public string Page => $"{Block.Page}쪽";
        public string Anchor => $"{(Memo.Panel == "TEXT" ? "텍스트" : "점자")} {Memo.LineIndex + 1}줄";
        public string State => Memo.Resolved ? "해결" : "미해결";
        public string Label => $"{Page} · {Anchor} · {State} · {Memo.Body}";
    }

    public NavigationLocation? Destination { get; private set; }
    public string? DestinationMemoId { get; private set; }
    public bool IncludeResolved { get; private set; }
    internal ListBox MemoItems { get; } = new() { Background = Brushes.Transparent, BorderThickness = new(0), Padding = new(0) };
    internal CheckBox ShowResolved { get; } = new() { Content = "해결한 메모 포함", Height = 20, FontSize = 14, Margin = new(0) };

    public MemoListDialog(DocumentSession model, string? selectedMemoId = null, bool includeResolved = false)
        : base("메모 목록", 640, 316, EditorModalDesign.Icon("form-close", assemblyName: "Semojum.UI.Common"))
    {
        AlignLabelBaseline();
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/Semojum.Feature.Editor.Wpf;component/Styles/MemoViews.xaml", UriKind.Relative) });
        ShowResolved.Template = (ControlTemplate)FindResource("MemoListCheck");
        ShowResolved.HorizontalAlignment = HorizontalAlignment.Left;
        ShowResolved.IsChecked = includeResolved;
        Fields.Children.Add(ShowResolved);
        var list = MemoItems;
        StripListPadding(list);
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(list, ScrollBarVisibility.Auto);
        list.ItemTemplate = (DataTemplate)FindResource("MemoListRow");
        var itemStyle = new Style(typeof(ListBoxItem), (Style)FindResource("MemoListItem"));
        itemStyle.Setters.Add(new Setter(AutomationProperties.NameProperty, new Binding(nameof(Item.Label))));
        list.ItemContainerStyle = itemStyle;
        AutomationProperties.SetName(list, "메모 목록");
        Fields.Children.Add(new Border { Margin = new(0, 12, 0, 0), Height = 154, CornerRadius = new(4), BorderThickness = new(1),
            BorderBrush = EditorModalDesign.Color("#EAEAEA"), Background = Brushes.White, Padding = new(0, 4, 0, 4), Child = list });
        var close = FooterButton("닫기", Close);
        close.IsCancel = true; close.Background = EditorModalDesign.Color("#FBFBFB"); close.BorderBrush = EditorModalDesign.Color("#D1D1D1");
        var move = FooterButton("이동", Open, true);
        move.IsEnabled = false;
        list.SelectionChanged += (_, _) => move.IsEnabled = list.SelectedItem is Item;
        list.MouseDoubleClick += (_, e) => { if (ItemsControl.ContainerFromElement(list, e.OriginalSource as DependencyObject) is ListBoxItem) Open(); };
        void Refresh()
        {
            var id = (list.SelectedItem as Item)?.Memo.LocalId ?? selectedMemoId;
            var entries = model.Blocks.OrderBy(b => b.Page).SelectMany(b => b.Memos.Where(m => ShowResolved.IsChecked == true || !m.Resolved)
                .OrderBy(m => m.LineIndex).Select(m => new Item(b, m))).ToArray();
            list.ItemsSource = entries;
            list.SelectedItem = entries.FirstOrDefault(x => x.Memo.LocalId == id) ?? entries.FirstOrDefault();
            if (list.SelectedItem is not null) list.ScrollIntoView(list.SelectedItem);

        }
        void Open()
        {
            if (list.SelectedItem is not Item item) return;
            var current = item.Block.Memos.FirstOrDefault(m => m.LocalId == item.Memo.LocalId);
            if (!model.Blocks.Contains(item.Block) || current is null || (current.Resolved && ShowResolved.IsChecked != true))
            { Status.Text = "메모 목록이 변경되었습니다."; Refresh(); return; }
            var panel = current.Panel == "TEXT" ? SearchPanel.Text : SearchPanel.Braille;
            var offsets = DocumentNavigation.LineOffsets(DocumentSearch.Content(item.Block, panel));
            if (current.LineIndex < 0 || current.LineIndex >= offsets.Length) { Status.Text = "편집으로 메모의 원래 줄이 사라졌습니다."; return; }
            IncludeResolved = ShowResolved.IsChecked == true; DestinationMemoId = current.LocalId;
            Destination = new(item.Block, panel, offsets[current.LineIndex]); DialogResult = true;
        }
        ShowResolved.Checked += (_, _) => Refresh(); ShowResolved.Unchecked += (_, _) => Refresh(); Refresh();
    }
}
