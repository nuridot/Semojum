using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Semojum.Desktop.Dialogs;

public abstract class ConversionFormDialog : FormDialogBase
{
    protected ConversionFormDialog(string title, double width, double height)
        : base(title, width, height, EditorModalDesign.Icon("form-close", assemblyName: "Semojum.UI.Common"))
    {
        AlignLabelBaseline();
        UseControlBorder();
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/Semojum.UI.Common;component/Controls/CompactRadio.xaml", UriKind.Relative) });
    }

    protected sealed class Pair : StackPanel
    {
        public RadioButton First { get; }
        public RadioButton Second { get; }
        public bool FirstSelected { get => First.IsChecked == true; set => (value ? First : Second).IsChecked = true; }

        public Pair(string name, string first, string second)
        {
            Orientation = Orientation.Horizontal;
            First = new() { Content = new TextBlock { Text = first }, Foreground = EditorModalDesign.Color("#1A1A1A"), Height = 20, Margin = new(0, 0, 16, 0) };
            Second = new() { Content = new TextBlock { Text = second }, Foreground = EditorModalDesign.Color("#1A1A1A"), Height = 20 };
            AutomationProperties.SetName(First, name + " " + first);
            AutomationProperties.SetName(Second, name + " " + second);
            Children.Add(First); Children.Add(Second);
        }
    }

    protected static FrameworkElement SelectField(ComboBox select, string name) =>
        SelectField(select, name, EditorModalDesign.Icon("conversion-chevron", assemblyName: "Semojum.Feature.Settings.Wpf"));
}
