using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Semojum.Desktop.Services;

internal static class WindowsGridIsolationChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var keys = new[] { "BoardListGrid", "UsageJobsGrid", "InstitutionListGrid", "InstitutionJobsGrid", "AppliedRulesGrid" };
        var grids = keys.Select(key => new DataGrid { Style = (Style)Application.Current.FindResource(key) }).ToArray();
        check(grids.Select(grid => grid.Style).Distinct().Count() == keys.Length
            && grids.Select(grid => grid.ColumnHeaderStyle).Distinct().Count() == keys.Length
            && grids.Select(grid => grid.CellStyle).Distinct().Count() == keys.Length,
            "Each screen owns its table, header and cell styles");
        var originalColors = grids.Select(grid => ((SolidColorBrush)new DataGridColumnHeader { Style = grid.ColumnHeaderStyle }.Background).Color).ToArray();
        var boardOverride = new Style(typeof(DataGridColumnHeader), grids[0].ColumnHeaderStyle);
        boardOverride.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.HotPink));
        grids[0].ColumnHeaderStyle = boardOverride;
        var changedHeader = new DataGridColumnHeader { Style = grids[0].ColumnHeaderStyle };
        check(changedHeader.Background == Brushes.HotPink && grids.Skip(1).Select(grid =>
                ((SolidColorBrush)new DataGridColumnHeader { Style = grid.ColumnHeaderStyle }.Background).Color).SequenceEqual(originalColors.Skip(1)),
            "A board-only header change leaves other screen headers unchanged");
        check(grids.All(grid => grid.Style.BasedOn is { } common && common.Setters.OfType<Setter>().All(setter =>
                setter.Property != Control.BackgroundProperty && setter.Property != DataGrid.ColumnHeaderStyleProperty
                && setter.Property != DataGrid.CellStyleProperty && setter.Property != DataGrid.HorizontalGridLinesBrushProperty)),
            "Shared table behavior does not own screen colors or cell styles");
    }
}
