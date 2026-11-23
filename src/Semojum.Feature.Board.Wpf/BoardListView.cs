using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Globalization;
using System.Windows.Media;

namespace Semojum.Feature.Board.Presentation;

/// <summary>Board-owned list presentation; the host supplies its screen-specific style.</summary>
public sealed class BoardListView : DataGrid
{
    public BoardListView(Style gridStyle)
    {
        AutoGenerateColumns = false; IsReadOnly = true; CanUserAddRows = false;
        SelectionMode = DataGridSelectionMode.Single; RowHeight = 48; Style = gridStyle;
        AlternationCount = int.MaxValue;
        var noticeBackground = new MultiBinding { Converter = new NoticeBackgroundConverter() };
        noticeBackground.Bindings.Add(new Binding(nameof(BoardRow.NoticeId)));
        noticeBackground.Bindings.Add(new Binding { Path = new PropertyPath("(0)", ItemsControl.AlternationIndexProperty), RelativeSource = RelativeSource.Self });
        RowStyle = new Style(typeof(DataGridRow));
        RowStyle.Setters.Add(new Setter(Control.BackgroundProperty, noticeBackground));
        Columns.Add(new DataGridTemplateColumn { Header = "번호", Width = 84,
            CellTemplate = (DataTemplate)Application.Current.FindResource("BoardNumber") });
        var title = new Style(typeof(TextBlock), DataGridTextColumn.DefaultElementStyle);
        title.Setters.Add(new Setter(TextBlock.FontSizeProperty, 14d));
        title.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
        title.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
        var regular = new DataTrigger { Binding = new Binding(nameof(BoardRow.NoticeId)), Value = null };
        regular.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Normal));
        title.Triggers.Add(regular);
        Columns.Add(new DataGridTextColumn { Header = "제목", Binding = new Binding(nameof(BoardRow.DisplayTitle)), ElementStyle = title,
            Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 160 });
        var metadata = new Style(typeof(TextBlock), DataGridTextColumn.DefaultElementStyle);
        metadata.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
        metadata.Setters.Add(new Setter(TextBlock.FontSizeProperty, 12d));
        metadata.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(95, 95, 95))));
        metadata.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
        foreach (var (label, property, width) in new[] { ("글쓴이", nameof(BoardRow.Author), 122d), ("작성일", nameof(BoardRow.CreatedAt), 112d), ("조회", nameof(BoardRow.ViewCount), 88d) })
            Columns.Add(new DataGridTextColumn { Header = label, Binding = new Binding(property), Width = width, ElementStyle = metadata });
    }
    private sealed class NoticeBackgroundConverter : IMultiValueConverter
    {
        private static readonly Brush Highlight = new SolidColorBrush(Color.FromRgb(249, 249, 249));
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            => values is [string { Length: > 0 }, int index] && index < 2 ? Highlight : Brushes.White;
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }}
