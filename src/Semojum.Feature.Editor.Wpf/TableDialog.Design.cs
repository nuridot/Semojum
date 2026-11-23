using System.Windows;
using AutomationProperties = System.Windows.Automation.AutomationProperties;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shell;
using SharpVectors.Converters;

namespace Semojum.Desktop.Dialogs;

public sealed partial class TableDialog
{
    private static SvgViewbox TableIcon(string icon) => EditorModalDesign.Icon("table-" + icon, assemblyName: "Semojum.Feature.Editor.Wpf");

    private static void StyleTool(ButtonBase button, string label, string icon)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(8, 0, 8, 0) };
        content.Children.Add(TableIcon(icon));
        content.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new(4, 0, 0, 0) });
        button.Content = content;
        button.MinWidth = 0; button.Height = button.MinHeight = 28;
        button.Background = button.BorderBrush = Brushes.Transparent; button.BorderThickness = new(1);
        button.Padding = new(0); button.Margin = new(0, 0, 4, 0); button.FontSize = 12;
        button.Template = EditorModalDesign.ButtonTemplate(button is ToggleButton ? typeof(ToggleButton) : typeof(Button));
        AutomationProperties.SetName(button, label);
    }

    private static void StyleGrid(DataGrid grid)
    {
        grid.HeadersVisibility = DataGridHeadersVisibility.Column;
        grid.RowHeight = grid.ColumnHeaderHeight = 32;
        grid.ColumnWidth = new DataGridLength(1, DataGridLengthUnitType.Star); grid.MinColumnWidth = 72;
        grid.CanUserSortColumns = grid.CanUserResizeRows = false;
        grid.Background = Brushes.White; grid.BorderBrush = EditorModalDesign.Color("#D1D1D1"); grid.BorderThickness = new(1);
        grid.GridLinesVisibility = DataGridGridLinesVisibility.All;
        grid.HorizontalGridLinesBrush = grid.VerticalGridLinesBrush = EditorModalDesign.Color("#EAEAEA");
        var header = new Style(typeof(DataGridColumnHeader));
        header.Setters.Add(new Setter(BackgroundProperty, EditorModalDesign.Color("#F9F9F9")));
        header.Setters.Add(new Setter(PaddingProperty, new Thickness(8, 0, 8, 0)));
        header.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        header.Setters.Add(new Setter(BorderBrushProperty, EditorModalDesign.Color("#EAEAEA")));
        grid.ColumnHeaderStyle = header;
        var text = new Style(typeof(TextBlock));
        text.Setters.Add(new Setter(MarginProperty, new Thickness(8, 0, 8, 0)));
        text.Setters.Add(new Setter(VerticalAlignmentProperty, VerticalAlignment.Center));
        grid.AutoGeneratingColumn += (_, e) => { if (e.Column is DataGridTextColumn column) column.ElementStyle = text; };
    }

    private void AddHeaderToggle(Panel toolbar, DataGrid grid, bool active)
    {
        toolbar.Children.Add(new Border { Width = 1, Height = 20, Background = EditorModalDesign.Color("#EAEAEA"), VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 4, 0) });
        var header = new ToggleButton { IsChecked = active };
        StyleTool(header, "머리행", "headrow"); toolbar.Children.Add(header);
        var style = new Style(typeof(DataGridRow));
        var first = new MultiDataTrigger();
        first.Conditions.Add(new Condition(new Binding(nameof(ToggleButton.IsChecked)) { Source = header }, true));
        first.Conditions.Add(new Condition(new Binding { Path = new PropertyPath("(0)", ItemsControl.AlternationIndexProperty), RelativeSource = RelativeSource.Self }, 0));
        first.Setters.Add(new Setter(BackgroundProperty, EditorModalDesign.Color("#EFF6FC")));
        first.Setters.Add(new Setter(BorderBrushProperty, EditorModalDesign.Color("#005FB8")));
        first.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(0, 0, 0, 2)));
        first.Setters.Add(new Setter(FontWeightProperty, FontWeights.SemiBold));
        first.Setters.Add(new Setter(AutomationProperties.ItemStatusProperty, "머리행"));
        style.Triggers.Add(first);
        grid.AlternationCount = int.MaxValue; grid.RowStyle = style;
        void Update()
        {
            HasHeaderRow = header.IsChecked == true;
            header.Background = HasHeaderRow ? EditorModalDesign.Color("#EFF6FC") : Brushes.Transparent;
            header.BorderBrush = HasHeaderRow ? EditorModalDesign.Color("#005FB8") : Brushes.Transparent;
        }
        header.Checked += (_, _) => Update(); header.Unchecked += (_, _) => Update(); Update();
    }

    private void StyleFrame(Button preview, Button cancel, Button apply)
    {
        EditorModalDesign.Frame(this, Root, Actions);
        if (Content is Grid frame && frame.Children[0] is Border { Child: DockPanel heading })
        {
            frame.RowDefinitions[0].Height = new GridLength(56); WindowChrome.GetWindowChrome(this).CaptionHeight = 56;
            var buttons = heading.Children.OfType<StackPanel>().Single(); buttons.Margin = new Thickness(0, 0, 16, 0);
            buttons.Children.OfType<Button>().Single().Content = TableIcon("cancel");
        }
        if (Actions.Parent is Border footer)
        {
            Actions.Children.Clear();
            var right = new StackPanel { Orientation = Orientation.Horizontal }; right.Children.Add(cancel); right.Children.Add(apply);
            var strip = new DockPanel { Height = 64, Margin = new(24, 0, 24, 0) };
            DockPanel.SetDock(right, Dock.Right); strip.Children.Add(right);
            preview.HorizontalAlignment = HorizontalAlignment.Left; preview.Margin = new(0); strip.Children.Add(preview);
            footer.Child = strip;
        }
    }
}
