using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Semojum.Desktop.Controls;

/// <summary>Visible attachment identity and removal, shared by both inquiry forms.</summary>
public static class AttachmentPresentation
{
    private static readonly Geometry Paperclip = Geometry.Parse("M12 5 L6 11 A2 2 0 0 1 3 8 L10 1 A3 3 0 0 1 14 5 L6 13 A4 4 0 0 1 0 7 L7 0");
    public static Path Icon() => new() { Data = Paperclip, Stroke = Brushes.DimGray, StrokeThickness = 1.4,
        Width = 16, Height = 16, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center };

    public static void Configure<T>(ListBox list, Action<T> remove, string id) where T : class
    {
        list.ClearValue(FrameworkElement.HeightProperty); list.MaxHeight = 160;
        list.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        var row = new FrameworkElementFactory(typeof(DockPanel));
        var button = new FrameworkElementFactory(typeof(Button));
        button.SetValue(DockPanel.DockProperty, Dock.Right);
        button.SetValue(ContentControl.ContentProperty, "삭제");
        button.SetValue(FrameworkElement.WidthProperty, 44d);
        button.SetValue(FrameworkElement.HeightProperty, 28d);
        button.SetValue(FrameworkElement.MinHeightProperty, 0d);
        button.SetValue(FrameworkElement.MinWidthProperty, 0d);
        button.SetValue(Control.PaddingProperty, new Thickness(4, 0, 4, 0));
        button.SetValue(AutomationProperties.AutomationIdProperty, id);
        button.SetBinding(AutomationProperties.NameProperty, new Binding("Name") { StringFormat = "첨부 삭제: {0}" });
        button.AddHandler(Button.ClickEvent, new RoutedEventHandler((sender, e) =>
        {
            if (sender is FrameworkElement { DataContext: T attachment }) remove(attachment);
            e.Handled = true;
        }));
        row.AppendChild(button);
        var icon = new FrameworkElementFactory(typeof(Path));
        icon.SetValue(DockPanel.DockProperty, Dock.Left); icon.SetValue(Path.DataProperty, Paperclip);
        icon.SetValue(Shape.StrokeProperty, Brushes.DimGray); icon.SetValue(Shape.StrokeThicknessProperty, 1.4);
        icon.SetValue(Shape.StretchProperty, Stretch.Uniform);
        icon.SetValue(FrameworkElement.WidthProperty, 16d); icon.SetValue(FrameworkElement.HeightProperty, 16d);
        icon.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        icon.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 0, 8, 0)); row.AppendChild(icon);
        var name = new FrameworkElementFactory(typeof(TextBlock));
        name.SetBinding(TextBlock.TextProperty, new Binding("Name")); name.SetBinding(FrameworkElement.ToolTipProperty, new Binding("Name"));
        name.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        name.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); row.AppendChild(name);
        list.ItemTemplate = new DataTemplate { VisualTree = row };
        var item = new Style(typeof(ListBoxItem));
        item.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        list.ItemContainerStyle = item;
    }
}
