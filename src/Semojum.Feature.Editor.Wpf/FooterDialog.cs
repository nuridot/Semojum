using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Semojum.Core;
using Semojum.Desktop.Controls;

namespace Semojum.Desktop.Dialogs;

public abstract class HeaderFooterInputDialog : FormDialogBase
{
    protected readonly StackPanel Rows = new();
    protected readonly TextBox TextField = new() { MaxLength = 200 };
    private readonly StackPanel alignment = new() { Orientation = Orientation.Horizontal };
    protected string SelectedAlignment => (string)alignment.Children.OfType<RadioButton>().Single(r => r.IsChecked == true).Tag;

    protected HeaderFooterInputDialog(string title, double height, string fieldName)
        : base(title, 520, height, EditorModalDesign.Icon("form-close", assemblyName: "Semojum.UI.Common"))
    {
        AlignLabelBaseline();
        UseControlBorder();
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/Semojum.UI.Common;component/Controls/CompactRadio.xaml", UriKind.Relative) });
        Fields.Children.Add(Card(Rows, new(16, 12, 16, 12)));
        FormRow(Rows, "내용 (200자)", Entry(TextField, fieldName), 122);
        TextField.Padding = new(10,0,11,0);
        TextField.Foreground = EditorModalDesign.Color("#1A1A1A");
    }

    protected void AddAlignment(string selected, params (string Caption, string Value)[] choices)
    {
        foreach (var (caption, value) in choices)
        {
            var radio = new RadioButton { Content = new TextBlock { Text = caption }, Height = 20, Tag = value,
                IsChecked = value == selected, Margin = new(alignment.Children.Count == 0 ? 0 : 16, 0, 0, 0) };
            AutomationProperties.SetName(radio, "정렬 " + caption);
            alignment.Children.Add(radio);
        }
        FormRow(Rows, "정렬", alignment, 122);
    }
}

public sealed class HeaderInputDialog : HeaderFooterInputDialog
{
    public string Result => TextField.Text;
    public string Alignment => SelectedAlignment;

    public HeaderInputDialog(string initial) : base("머리말", 218, "머리말 내용")
    {
        var (text, align) = BrailleTags.ReadHeader(initial);
        TextField.Text = text;
        AddAlignment(align, ("왼쪽", "left"), ("가운데", "center"), ("오른쪽", "right"));
        CancelButton();
        FooterButton("확인", () =>
        {
            if (Result.Length > TextField.MaxLength) throw new ArgumentException("머리말 내용은 200자까지입니다.");
            if (Result.Contains("<!머리말>", StringComparison.Ordinal) || Result.Contains("<!/머리말>", StringComparison.Ordinal)) throw new ArgumentException("머리말 안에 머리말 태그를 넣을 수 없습니다.");
            if (System.Text.RegularExpressions.Regex.IsMatch(Result, "<!(왼쪽|가운데|오른쪽)정렬>")) throw new ArgumentException("머리말 정렬은 정렬 단추로 골라 주세요.");
            DialogResult = true;
        }, true);
    }
}

public sealed class FooterInputDialog : HeaderFooterInputDialog
{
    public FooterRange? Result { get; private set; }

    public FooterInputDialog(int page = 1) : base("꼬리말", 262, "내용")
    {
        AddAlignment("center", ("가운데", "center"), ("오른쪽", "right"), ("왼쪽", "left"));
        var start = new TextBox { Text = Math.Max(1, page).ToString() };
        var number = new StackPanel { Orientation = Orientation.Horizontal };
        number.Children.Add(NumberField(start, "시작 면 번호", 1, int.MaxValue));
        number.Children.Add(new TextBlock { Text = "면부터", FontSize = 12, Margin = new(8, 0, 0, 0),
            Foreground = EditorModalDesign.Color("#5F5F5F"), VerticalAlignment = VerticalAlignment.Center });
        FormRow(Rows, "시작 면 번호", number, 122);
        CancelButton();
        FooterButton("확인", () =>
        {
            if (!int.TryParse(start.Text, out var pageNumber)) throw new ArgumentException("시작 면 번호를 입력해 주세요.");
            var value = new FooterRange(pageNumber, null, TextField.Text, SelectedAlignment);
            FooterRange.Validate([value]);
            Result = value;
            DialogResult = true;
        }, true);
    }
}

public sealed class FooterDialog : FormDialogBase
{
    public new sealed class Entry : INotifyPropertyChanged
    {
        private string start = "1", end = "끝";
        public string Start { get => start; set { if (start == value) return; start = value; PropertyChanged?.Invoke(this, new(nameof(Start))); } }
        public string End { get => end; internal set { if (end == value) return; end = value; PropertyChanged?.Invoke(this, new(nameof(End))); } }
        internal bool RestoresDefault { get; init; }
        public string Alignment { get; set; } = "기본";
        public string Text { get; set; } = "";
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private const string Assets = "Semojum.Feature.Editor.Wpf";
    private readonly ObservableCollection<Entry> entries;
    private readonly DataGrid grid;
    public string Footer { get; private set; } = "";
    public string Alignment { get; }
    public FooterRange[] Ranges { get; private set; } = [];

    public FooterDialog(DocumentSession session) : base("꼬리말 관리", 720, 380, EditorModalDesign.Icon("form-close", assemblyName: "Semojum.UI.Common"))
    {
        AlignLabelBaseline();
        UseControlBorder();
        Alignment = session.Layout.FooterAlign;
        var points = FooterRange.ChangePoints(session.FooterRanges, session.FooterText, session.Layout.FooterAlign);
        if (points.Length == 0 && session.FooterText.Length > 0) points = [new(1, null, session.FooterText)];
        var storedStarts = session.FooterRanges.Select(r => r.Start).ToHashSet();
        entries = new(points.Select(r => new Entry { Start = r.Start.ToString(), Text = r.Text,
            RestoresDefault = !storedStarts.Contains(r.Start), Alignment = !storedStarts.Contains(r.Start) ? "기본" : r.Alignment switch { "left" => "왼쪽", "right" => "오른쪽", "center" => "가운데", _ => "기본" } }));
        var originalRows = entries.Select(e => (e.Start, e.Text, e.Alignment)).ToArray();
        foreach (var entry in entries) entry.PropertyChanged += EntryChanged;
        entries.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is not null) foreach (Entry entry in e.NewItems) entry.PropertyChanged += EntryChanged;
            if (e.OldItems is not null) foreach (Entry entry in e.OldItems) entry.PropertyChanged -= EntryChanged;
            UpdateEnds();
        };
        UpdateEnds();
        var line = EditorModalDesign.Color("#EAEAEA");
        grid = new DataGrid
        {
            ItemsSource = entries, AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false,
            CanUserSortColumns = false, CanUserReorderColumns = false, CanUserResizeRows = false, CanUserResizeColumns = false,
            IsReadOnly = true, HeadersVisibility = DataGridHeadersVisibility.Column, RowHeaderWidth = 0,
            BorderThickness = new(0), Background = Brushes.White, RowBackground = Brushes.White, AlternatingRowBackground = Brushes.White,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, HorizontalGridLinesBrush = line,
            RowHeight = 44, ColumnHeaderHeight = 32, MaxHeight = 208, MinRowHeight = 0,
            SelectionMode = DataGridSelectionMode.Single, SelectionUnit = DataGridSelectionUnit.FullRow,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        AutomationProperties.SetName(grid, "꼬리말 범위");
        var header = new Style(typeof(DataGridColumnHeader));
        header.Setters.Add(new Setter(Control.BackgroundProperty, EditorModalDesign.Color("#F9F9F9")));
        header.Setters.Add(new Setter(Control.ForegroundProperty, EditorModalDesign.Color("#5F5F5F")));
        header.Setters.Add(new Setter(Control.FontSizeProperty, 12d));
        header.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Normal));
        header.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        header.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        header.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
        grid.ColumnHeaderStyle = header;
        var cell = new Style(typeof(DataGridCell));
        cell.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        cell.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
        cell.Setters.Add(new Setter(Control.ForegroundProperty, EditorModalDesign.Color("#1A1A1A")));
        cell.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));
        cell.Setters.Add(new Setter(Control.IsTabStopProperty, false));
        grid.CellStyle = cell;
        var startColumn = Column("시작 면", 116, () =>
        {
            var box = BoundBox(nameof(Entry.Start));
            var spinner = NumberField(box, "시작 면 번호", 1, int.MaxValue);
            spinner.Margin = new(12, 0, 8, 0);
            return spinner;
        });
        startColumn.Header = new TextBlock { Text = "시작 면", Margin = new(12,0,0,0) };
        Column("끝 면", 80, () =>
        {
            var end = new TextBlock { FontSize = 14, Foreground = EditorModalDesign.Color("#5F5F5F"),
                VerticalAlignment = VerticalAlignment.Center, ToolTip = "다음 시작 면 바로 앞까지 적용됩니다." };
            AutomationProperties.SetName(end, "자동 계산된 끝 면 번호");
            end.SetBinding(TextBlock.TextProperty, new Binding(nameof(Entry.End)) { Mode = BindingMode.OneWay });
            return end;
        });
        var textColumn = Column("내용 (200자)", new DataGridLength(1, DataGridLengthUnitType.Star), () =>
        {
            var box = BoundBox(nameof(Entry.Text));
            box.MaxLength = 200;
            var field = base.Entry(box, "꼬리말 내용");
            box.Padding = new(10,0,11,0);
            box.Foreground = EditorModalDesign.Color("#1A1A1A");
            field.Margin = new(0, 0, 8, 0);
            return field;
        });
        textColumn.MinWidth = 148;
        Column("정렬", 140, () =>
        {
            var select = new ComboBox { ItemsSource = new[] { "기본", "가운데", "오른쪽", "왼쪽" } };
            select.SetBinding(ComboBox.SelectedItemProperty, Bind(nameof(Entry.Alignment)));
            var field = SelectField(select, "꼬리말 정렬", EditorModalDesign.Icon("line-next", assemblyName: Assets));
            field.Margin = new(0, 0, 8, 0);
            return field;
        });
        Column("", 24, () =>
        {
            var remove = new Button();
            remove.Width = 24; remove.Height = 28; remove.MinHeight = remove.MinWidth = 0;
            remove.Padding = new(0); remove.Margin = new(0);
            var trash = EditorModalDesign.Icon("table-trash", assemblyName: Assets);
            trash.RenderTransform = new TranslateTransform(2,0);
            remove.Content = trash;
            remove.ToolTip = "이 꼬리말 삭제";
            remove.Background = Brushes.Transparent; remove.BorderThickness = new(0);
            remove.Template = EditorModalDesign.ButtonTemplate(typeof(Button));
            AutomationProperties.SetName(remove, "이 꼬리말 삭제");
            remove.Click += (_, _) => { if (remove.DataContext is Entry entry) entries.Remove(entry); };
            return remove;
        });

        var viewport = new ScrollViewer { Content = grid, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };
        AutomationProperties.SetName(viewport, "꼬리말 표 가로 스크롤");
        // Keep numeric and alignment fields usable when the monitor is narrower than the table.
        // Reserve room for a vertical scrollbar when there are more than four rows.
        void FitColumns() => grid.Width = Math.Max(508 + (entries.Count > 4 ? SystemParameters.VerticalScrollBarWidth : 0), viewport.ActualWidth);
        viewport.SizeChanged += (_, _) => FitColumns();
        entries.CollectionChanged += (_, _) => FitColumns();
        var table = new StackPanel();
        table.Children.Add(viewport);
        var add = Button("꼬리말 줄 더하기", () => RunAsync(Add));
        var addLabel = new StackPanel { Orientation = Orientation.Horizontal };
        addLabel.Children.Add(EditorModalDesign.Icon("table-plus", assemblyName: Assets));
        addLabel.Children.Add(new TextBlock { Text = "꼬리말 줄 더하기", FontSize = 12, Margin = new(8,0,0,0),
            Foreground = EditorModalDesign.Color("#5F5F5F"), VerticalAlignment = VerticalAlignment.Center });
        add.Content = addLabel; add.Margin = add.Padding = new(0);
        add.Height = 40; add.Background = Brushes.White; add.BorderThickness = new(0);
        add.Template = EditorModalDesign.ButtonTemplate(typeof(Button));
        AutomationProperties.SetName(add, "꼬리말 줄 더하기");
        table.Children.Add(add);
        Fields.Children.Add(new Border { Background = Brushes.White, BorderBrush = line, BorderThickness = new(1), CornerRadius = new(4), Child = table });
        LeadingFooterButton("모두 지우기", () => { entries.Clear(); return Task.CompletedTask; });
        CancelButton();
        FooterButton("확인", () =>
        {
            if (entries.Select(e => (e.Start, e.Text, e.Alignment)).SequenceEqual(originalRows))
            {
                Footer = session.FooterText;
                Ranges = session.FooterRanges;
                DialogResult = true;
                return;
            }
            var ranges = entries.Select(e =>
            {
                if (!int.TryParse(e.Start, out var start)) throw new ArgumentException("시작 면 번호를 정수로 입력해 주세요.");
                return new FooterRange(start, null, e.Text, e.Alignment switch { "왼쪽" => "left", "오른쪽" => "right", "가운데" => "center", _ => null });
            }).ToArray();
            var fallbackStarts = entries.Where(e => e.RestoresDefault && e.Text == session.FooterText && e.Alignment == "기본")
                .Select(e => int.Parse(e.Start)).ToHashSet();
            Ranges = FooterRange.Continue(ranges, fallbackStarts);
            Footer = Ranges.Length < ranges.Length ? session.FooterText :
                Ranges.FirstOrDefault(r => r.Start == 1)?.Text ?? (Ranges.Length == 0 ? "" : session.FooterText);
            DialogResult = true;
        }, true);
    }

    private DataGridColumn Column(string title, DataGridLength width, Func<FrameworkElement> view)
    {
        var column = new FieldColumn(view) { Header = title, Width = width };
        grid.Columns.Add(column);
        return column;
    }
    private sealed class FieldColumn(Func<FrameworkElement> view) : DataGridColumn
    {
        protected override FrameworkElement GenerateElement(DataGridCell cell, object item)
        {
            var element = view();
            // Each 44-DIP row includes a 1-DIP separator, leaving a half-pixel vertical center.
            element.SnapsToDevicePixels = false;
            element.RenderTransform = new TranslateTransform(0,-.5);
            return element;
        }
        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object item) => GenerateElement(cell,item);
    }
    private static Binding Bind(string path) => new(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
    private static TextBox BoundBox(string path)
    {
        var box = new TextBox();
        box.SetBinding(TextBox.TextProperty, Bind(path));
        return box;
    }
    private void EntryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Entry.Start)) UpdateEnds();
    }
    private void UpdateEnds()
    {
        var sorted = entries.OrderBy(e => int.TryParse(e.Start, out var n) && n >= 1 ? n : long.MaxValue).ToArray();
        for (var i = 0; i < sorted.Length; i++)
        {
            sorted[i].End = i + 1 == sorted.Length ? "끝" : int.TryParse(sorted[i+1].Start, out var next) && next > 1
                ? (next-1).ToString() : "—";
        }
    }
    private Task Add()
    {
        var last = entries.Select(e => int.TryParse(e.Start, out var page) ? page : 0).DefaultIfEmpty(0).Max();
        if (last == int.MaxValue) throw new ArgumentException("마지막 시작 면 번호 다음에 줄을 추가할 수 없습니다.");
        if (entries.Count >= 2001) throw new ArgumentException("꼬리말 범위는 1,000개까지 지정할 수 있습니다.");
        var entry = new Entry { Start = (Math.Max(last,0)+1).ToString() };
        entries.Add(entry);
        grid.SelectedItem = entry;
        grid.ScrollIntoView(entry);
        return Task.CompletedTask;
    }
}
