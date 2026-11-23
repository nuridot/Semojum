using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Semojum.Core;
using Semojum.Desktop.Controls;
using Semojum.Desktop.Dialogs;
using SharpVectors.Converters;

namespace Semojum.Desktop.Services;

internal static class WindowsFooterFormulaDesignChecks
{
    internal static void Run(Window owner, Action<bool, string> check)
    {
        var header = new HeaderInputDialog("선택한 내용") { Owner = owner }; header.Show();
        try
        {
            header.UpdateLayout();
            var choices = Children(header).OfType<RadioButton>().ToArray();
            check(header.Width == 520 && header.Height == 218 && header.Result == "선택한 내용", "Header dialog preserves selected text and Figma geometry");
            check(header.Alignment == "center" && choices.Select(c => c.Tag).SequenceEqual(new[] { "left", "center", "right" }) && choices.All(c => c.IsEnabled),
                "Header alignment offers left, center and right like the final Figma, centered by default");
            choices[0].IsChecked = true; var left = header.Alignment; choices[2].IsChecked = true;
            check(left == "left" && header.Alignment == "right" && BrailleTags.InsertHeader(header.Result, header.Alignment) == "<!머리말><!오른쪽정렬>선택한 내용<!/머리말>",
                "The chosen header alignment is written into the header tag for the engine");
            CheckIcons(header, check);
        }
        finally { header.Close(); }
        var aligned = new HeaderInputDialog("<!왼쪽정렬>Ⅰ 01") { Owner = owner }; aligned.Show();
        try { check(aligned.Result == "Ⅰ 01" && aligned.Alignment == "left", "Editing an aligned header reopens with its text and alignment"); }
        finally { aligned.Close(); }

        var math = new MathDialog((_, _) => Task.FromResult(BrailleTranslation.Plain("⠁")), "x+1", "⠿") { Owner = owner }; math.Show();
        try
        {
            math.UpdateLayout(); math.RefreshAsync().GetAwaiter().GetResult();
            check(math.Width == 900 && math.Height == 558 && math.BrailleInput.Text == "⠿" && math.Edited, "The 900×558 math editor preserves manual braille");
            math.Input.Text = "x+2"; math.RefreshAsync().GetAwaiter().GetResult();
            check(math.BrailleInput.Text == "⠿" && math.Edited, "Changing LaTeX does not replace manual braille"); CheckIcons(math, check);
            var fraction = math.SymbolButtons.Children.OfType<Button>().Single(b => AutomationProperties.GetName(b) == "분수");
            var root = math.SymbolButtons.Children.OfType<Button>().Single(b => AutomationProperties.GetName(b) == "제곱근");
            static bool ColorIs(Brush? brush, byte r, byte g, byte b) => brush is SolidColorBrush solid && solid.Color == Color.FromRgb(r, g, b);
            check(math.SymbolButtons.Children.Count == 22 && math.TemplateButtons.Children.Count == 12
                && math.SymbolButtons.Children.OfType<Button>().All(b => b.ActualHeight == 28)
                && math.TemplateButtons.Children.OfType<Button>().All(b => b.ActualHeight == 56),
                "The Figma math rows offer 22 quick symbols and all twelve template menus at 28 and 56 DIP");
            check(ColorIs(fraction.Background, 255, 255, 255) && ColorIs(fraction.BorderBrush, 209, 209, 209), "An unselected math symbol uses the Figma white fill and gray border");
            fraction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(ColorIs(fraction.Background, 239, 246, 252) && ColorIs(fraction.BorderBrush, 0, 95, 184) && ColorIs(root.Background, 255, 255, 255), "Selecting a math symbol uses the Figma blue fill and border");
            root.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(ColorIs(fraction.Background, 255, 255, 255) && ColorIs(fraction.BorderBrush, 209, 209, 209) && ColorIs(root.Background, 239, 246, 252), "Choosing another math symbol restores the previous symbol's unselected state");
        }
        finally { math.Close(); }
    }
    private static void CheckIcons(Window window, Action<bool, string> check)
    {
        var icons = Children(window).OfType<SvgViewbox>().ToArray();
        check(icons.Length > 0 && icons.All(icon => icon.Drawings is { Bounds.IsEmpty: false } && icon.ActualWidth > 0 && icon.ActualHeight > 0 && icon.ActualWidth <= icon.Width + 1 && icon.ActualHeight <= icon.Height + 1), $"{window.Title} Figma SVGs render in their intended slots");
    }
    private static IEnumerable<DependencyObject> Children(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var nested in Children(child)) yield return nested; }
    }
}
