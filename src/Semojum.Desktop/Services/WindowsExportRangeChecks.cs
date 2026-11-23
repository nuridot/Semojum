using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Semojum.Core;
using Semojum.Desktop.Dialogs;

namespace Semojum.Desktop.Services;

internal static class WindowsExportRangeChecks
{
    internal static void Run(Window owner, Action<bool, string> check)
    {
        var first = new ExportRangeDialog([3, 4, 6], () => Task.FromResult(4), allowFormatChange: true) { Owner = owner };
        Inspect(first, () =>
        {
            check(Choice(first, "원본 페이지").IsChecked == true && Choice(first, "점자 페이지").IsEnabled,
                "Range save offers source-page and braille-face scopes");
            Choice(first, "점자 페이지").IsChecked = true;
            check(Choice(first, ".brf").IsChecked == true && Choice(first, ".txt 점자").IsEnabled, "Braille range offers BRF and braille TXT");
            Choice(first, "원본 페이지").IsChecked = true;
            check(Choice(first, ".txt 원문").IsChecked == true && Choice(first, ".md").IsEnabled, "Original range offers source TXT and Markdown");
            Choice(first, ".md").IsChecked = true;
            check(first.SelectedFormat == ".md", "Original range records Markdown selection");
            check(Field(first, true).Text == "3" && Field(first, false).Text == "6", "Source-page bounds initialize from the document");
            Field(first, true).Text = "4";
        });
        check(first.Result == new ExportRange(ExportScope.OriginalPages, 4, 6), "Confirming source-page selection yields the chosen range");

        var only = new ExportRangeDialog([], () => Task.FromResult(4), brailleOnly: true) { Owner = owner };
        Inspect(only, () =>
        {
            check(Choice(only, "점자 페이지").IsChecked == true && !Choice(only, "원본 페이지").IsEnabled,
                "Preview export starts in braille scope with the source scope disabled");
            check(Field(only, true).Text == "1" && Field(only, false).Text == "4", "Braille bounds use composed physical faces");
            Field(only, true).Text = "2"; Field(only, false).Text = "3";
        });
        check(only.Result == new ExportRange(ExportScope.BraillePages, 2, 3), "Preview selection yields the actual chosen face range");
    }

    private static void Inspect(ExportRangeDialog dialog, Action inspect)
    {
        Exception? error = null;
        dialog.ContentRendered += (_, _) =>
        {
            try { inspect(); Save(dialog).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
            catch (Exception ex) { error = ex; dialog.Close(); }
        };
        var accepted = dialog.ShowDialog();
        if (error is not null) throw new InvalidOperationException("Range dialog interaction failed", error);
        if (accepted != true) throw new InvalidOperationException("Range dialog did not accept valid settings");
    }

    internal static RadioButton Choice(Window dialog, string label) =>
        Tree(dialog).OfType<RadioButton>().Single(r => AutomationProperties.GetName(r) == label);
    internal static TextBox Field(Window dialog, bool first) =>
        Tree(dialog).OfType<TextBox>().Single(f => AutomationProperties.GetName(f) == (first ? "시작 페이지 번호" : "끝 페이지 번호"));
    internal static Button Save(Window dialog) => Tree(dialog).OfType<Button>().Single(b => Equals(b.Content, "저장"));
    internal static IEnumerable<DependencyObject> Tree(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in Tree(child)) yield return nested;
        }
    }
}
