using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Semojum.Core;
using Semojum.Core.Integration;

namespace Semojum.Desktop.Dialogs;
public sealed class TableDialog : CompactDialog
{
    public string SourceText { get; private set; } = "";
    public BrailleTranslation? Translation { get; private set; }
    public TableDialog(string source, BlockMetadata metadata, BrailleOptions options, LocalBrailleEngine engine) : base("표 구조 편집", 760, 620)
    {
        var cells = TaggedTable.Read(source); var table = new DataTable();
        for (var i = 0; i < cells.Max(r => r.Length); i++) table.Columns.Add($"열 {i + 1}");
        foreach (var row in cells) table.Rows.Add(row.Cast<object>().ToArray());
        var root = new DockPanel { Margin = new(16) }; Body(root);
        var grid = new DataGrid { ItemsSource = table.DefaultView, AutoGenerateColumns = true, CanUserAddRows = false, CanUserDeleteRows = false,
            SelectionUnit = DataGridSelectionUnit.CellOrRowHeader, SelectionMode = DataGridSelectionMode.Extended, MinHeight = 180, Height = 260 };
        DataGridColumn? selectedColumn = null;
        DataRow[] selectedRows = [];
        void RememberSelection()
        {
            if (grid.CurrentCell.IsValid) selectedColumn = grid.CurrentCell.Column;
            selectedRows = grid.SelectedItems.OfType<DataRowView>().Select(r => r.Row)
                .Concat(grid.SelectedCells.Select(c => c.Item).OfType<DataRowView>().Select(r => r.Row)).Distinct().ToArray();
            if (selectedRows.Length == 0 && grid.CurrentItem is DataRowView current) selectedRows = [current.Row];
        }
        grid.CurrentCellChanged += (_, _) => RememberSelection();
        grid.SelectedCellsChanged += (_, _) => RememberSelection();
        grid.PreviewMouseLeftButtonDown += (_, e) =>
        {
            for (var hit = e.OriginalSource as DependencyObject; hit is not null; hit = VisualTreeHelper.GetParent(hit))
                if (hit is DataGridColumnHeader { Column: { } column }) { selectedColumn = column; break; }
        };
        var preview = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = (System.Windows.Media.FontFamily)FindResource("BrailleFont"), FontSize = 22, Margin = new(0, 12, 0, 0) };
        bool Commit() => grid.CommitEdit(DataGridEditingUnit.Cell, true) && grid.CommitEdit(DataGridEditingUnit.Row, true);
        void RefreshColumns()
        {
            // WPF does not regenerate DataView columns when its schema changes.
            grid.ItemsSource = null;
            grid.Columns.Clear();
            grid.ItemsSource = table.DefaultView;
            selectedColumn = null; selectedRows = [];
        }
        var toolbar = new WrapPanel { Margin = new(0, 0, 0, 8) }; DockPanel.SetDock(toolbar, Dock.Top); root.Children.Add(toolbar);
        void StructureButton(string label, System.Action change)
        {
            toolbar.Children.Add(Button(label, () =>
            {
                try
                {
                    Status.Text = "";
                    if (Commit()) { change(); Translation = null; preview.Clear(); }
                    else Status.Text = "편집 중인 셀 내용을 확인해 주세요.";
                }
                catch (Exception ex) { Status.Text = ex.Message; }
                return Task.CompletedTask;
            }));
        }
        StructureButton("열 추가", () =>
        {
            if (table.Columns.Count >= 100) return;
            var number = 1;
            while (table.Columns.Contains("열 " + number)) number++;
            table.Columns.Add("열 " + number);
            RefreshColumns();
        });
        StructureButton("선택 열 삭제", () =>
        {
            if (table.Columns.Count <= 1) { Status.Text = "표에는 최소 한 열이 필요합니다."; return; }
            var column = selectedColumn ?? grid.CurrentColumn;
            if (column is null) { Status.Text = "삭제할 열의 셀이나 머리글을 선택해 주세요."; return; }
            // DisplayIndex changes when the user reorders columns; the binding field does not.
            var name = (column as DataGridBoundColumn)?.Binding is Binding binding ? binding.Path.Path : column.SortMemberPath;
            if (!table.Columns.Contains(name)) return;
            table.Columns.Remove(name);
            RefreshColumns();
        });
        StructureButton("행 추가", () =>
        {
            if (table.Rows.Count >= 100) return;
            var row = table.NewRow(); table.Rows.Add(row);
            grid.SelectedItem = table.DefaultView[table.Rows.IndexOf(row)];
            grid.ScrollIntoView(grid.SelectedItem);
        });
        StructureButton("선택 행 삭제", () =>
        {
            var rows = selectedRows.Where(r => r.Table == table && r.RowState != DataRowState.Detached).ToArray();
            if (rows.Length == 0 && grid.SelectedItem is DataRowView row) rows = [row.Row];
            if (rows.Length == 0) { Status.Text = "삭제할 행의 셀이나 머리글을 선택해 주세요."; return; }
            if (table.Rows.Count <= rows.Length) { Status.Text = "표에는 최소 한 행이 필요합니다."; return; }
            foreach (var selected in rows) table.Rows.Remove(selected);
            selectedRows = [];
        });
        DockPanel.SetDock(grid, Dock.Top); root.Children.Add(grid);
        root.Children.Add(preview);
        async Task Translate()
        {
            if (!Commit()) throw new InvalidOperationException("편집 중인 셀 내용을 확인해 주세요.");
            SourceText = TaggedTable.Write(table.Rows.Cast<DataRow>().Where(r => r.RowState != DataRowState.Deleted).Select(r => r.ItemArray.Select(v => v?.ToString() ?? "")));
            Translation = await engine.TranslateElementAsync(SourceText, metadata with { Type = "table", TableSource = SourceText }, options, Lifetime.Token);
            Translation = Translation with { SourceText = SourceText }; preview.Text = Translation.Cells;
        }
        Action("점자 미리보기", Translate); Action("적용", async () => { await Translate(); DialogResult = true; }, true); Action("취소", () => DialogResult = false);
    }
}
