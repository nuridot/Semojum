using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Semojum.Core;
using Semojum.Feature.Editor;

namespace Semojum.Desktop.Dialogs;

internal sealed class ReviewListDialog : FormDialogBase
{
    internal sealed record Item(EditorBlock Block, ReviewMark Mark)
    {
        public string Page => $"{Block.Page}쪽";
        public string Panel => Mark.Panel == ReviewPanel.Text ? "텍스트" : "점자";
        public string Kind => MainWindow.ReviewLabel(Mark.Kind);
        public string Detail => Mark.IsStale ? Mark.Kind == ReviewKind.OpenWarning ? "블록을 고쳐 풀림" : "다시 확인 필요" : Mark.OriginalText;
        public string Label => $"{Page} · {Panel} · {Kind} · {Detail}";
        public override string ToString() => Label;
    }

    internal Item? Destination { get; private set; }
    internal ListBox Marks { get; } = new() { BorderThickness = new(0), Background = Brushes.Transparent, Padding = new(0) };

    internal ReviewListDialog(DocumentSession model, Func<Item, string> reassign, Func<Item, string> remove)
        : base("검토 표시 목록", 640, 284, EditorModalDesign.Icon("form-close", assemblyName: "Semojum.UI.Common"))
    {
        AlignLabelBaseline();
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/Semojum.Feature.Editor.Wpf;component/Styles/MemoViews.xaml", UriKind.Relative) });
        StripListPadding(Marks);
        Marks.ItemTemplate = (DataTemplate)FindResource("ReviewListRow");
        var style = new Style(typeof(ListBoxItem), (Style)FindResource("MemoListItem"));
        style.Setters.Add(new Setter(AutomationProperties.NameProperty, new Binding(nameof(Item.Label))));
        style.Setters.Add(new Setter(ToolTipProperty, new Binding("Mark.OriginalText")));
        Marks.ItemContainerStyle = style;
        ScrollViewer.SetHorizontalScrollBarVisibility(Marks, ScrollBarVisibility.Disabled);
        AutomationProperties.SetName(Marks, "검토 표시 목록");
        AutomationProperties.SetHelpText(Marks, "표시를 선택해 이동합니다. F2로 다시 지정하고 Delete로 선택 삭제할 수 있습니다.");
        Fields.Children.Add(new Border { Height = 154, BorderThickness = new(1), BorderBrush = EditorModalDesign.Color("#EAEAEA"),
            CornerRadius = new(4), Background = Brushes.White, Padding = new(0,4,0,4), Child = Marks });
        var close = FooterButton("닫기", Close);
        close.IsCancel = true;
        close.Background = EditorModalDesign.Color("#FBFBFB");
        close.BorderBrush = EditorModalDesign.Color("#D1D1D1");
        var move = FooterButton("이동", Open, true);
        var menu = new ContextMenu();
        var assign = new MenuItem { Header = "다시 지정", InputGestureText = "F2" };
        var delete = new MenuItem { Header = "선택 삭제", InputGestureText = "Delete" };
        menu.Items.Add(assign);
        menu.Items.Add(delete);
        Marks.ContextMenu = menu;
        assign.Click += (_, _) => Edit(reassign);
        delete.Click += (_, _) => Edit(remove);
        Marks.PreviewMouseRightButtonDown += (_, e) =>
        {
            if (ItemsControl.ContainerFromElement(Marks, e.OriginalSource as DependencyObject) is ListBoxItem item)
                item.IsSelected = true;
        };
        Marks.SelectionChanged += (_, _) => UpdateActions();
        Marks.MouseDoubleClick += (_, e) =>
        {
            if (ItemsControl.ContainerFromElement(Marks, e.OriginalSource as DependencyObject) is ListBoxItem) Open();
        };
        Marks.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.F2 && assign.IsEnabled) { Edit(reassign); e.Handled = true; }
            if (e.Key == Key.Delete && delete.IsEnabled) { Edit(remove); e.Handled = true; }
        };
        Refresh();
        UpdateActions();

        Item? Current()
        {
            if (Marks.SelectedItem is not Item item || !model.Blocks.Contains(item.Block)) return null;
            var mark = item.Block.ReviewMarks.FirstOrDefault(m => m.Id == item.Mark.Id);
            return mark is null ? null : new Item(item.Block, mark);
        }
        void Refresh()
        {
            var previous = Marks.SelectedItem as Item;
            var items = model.Blocks.OrderBy(b => b.Page).SelectMany(b => b.ReviewMarks.Select(m => new Item(b,m))).ToArray();
            Marks.ItemsSource = items;
            Marks.SelectedItem = items.FirstOrDefault(i => i.Block == previous?.Block && i.Mark.Id == previous.Mark.Id) ?? items.FirstOrDefault();
            if (items.Length == 0) Status.Text = "검토 표시가 없습니다.";
        }
        void UpdateActions()
        {
            var item = Current();
            move.IsEnabled = item is not null;
            delete.IsEnabled = item is not null && !model.IsReadOnly;
            assign.IsEnabled = delete.IsEnabled && item!.Mark.IsStale && item.Mark.Kind != ReviewKind.OpenWarning;
        }
        void Edit(Func<Item,string> action)
        {
            if (Current() is { } current) Status.Text = action(current);
            else Status.Text = "검토 표시 목록이 변경되었습니다.";
            Refresh();
        }
        void Open()
        {
            Destination = Current();
            if (Destination is null) { Status.Text = "검토 표시 목록이 변경되었습니다."; Refresh(); return; }
            DialogResult = true;
        }
    }
}
