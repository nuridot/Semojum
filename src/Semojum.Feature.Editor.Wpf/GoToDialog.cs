using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Semojum.Core;

namespace Semojum.Desktop.Dialogs;

public sealed class GoToDialog : FormDialogBase
{
    internal TextBox Number { get; } = new() { Text = "1" };
    internal TextBlock Result { get; } = new()
    {
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center
    };

    public GoToDialog(SearchPanel panel, Func<NavigationTarget, string, Task<string>> navigate)
        : base("이동", 480, 218, EditorModalDesign.Icon("table-cancel", assemblyName: "Semojum.Feature.Editor.Wpf"))
    {
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/Semojum.UI.Common;component/Controls/CompactRadio.xaml", UriKind.Relative) });
        AlignLabelBaseline();
        var rows = new StackPanel();
        Fields.Children.Add(Card(rows, new(16, 12, 16, 12)));
        var choices = new StackPanel { Orientation = Orientation.Horizontal };
        AutomationProperties.SetName(choices, "이동할 대상");
        RadioButton Choice(string label)
        {
            var choice = new RadioButton { Content = new TextBlock { Text = label }, Height = 20, Margin = new(0, 0, 16, 0) };
            AutomationProperties.SetName(choice, label);
            choice.Checked += (_, _) => Result.Text = "";
            choices.Children.Add(choice);
            return choice;
        }
        var original = Choice("원본 쪽");
        var braille = Choice("점자 면");
        braille.ToolTip = "점자 면은 조판 결과를 첫 면부터 센 번호입니다.";
        (panel == SearchPanel.Braille ? braille : original).IsChecked = true;
        FormRow(rows, "이동할 대상", choices, 132);
        FormRow(rows, "번호", Entry(Number, "번호"), 132);
        Number.Padding = new(10, 0, 11, 0);
        Number.ToolTip = "+3 또는 -3처럼 상대 이동할 수 있습니다. 위·아래 방향키로 번호를 조절합니다.";
        Number.PreviewKeyDown += (_, e) =>
        {
            if (Keyboard.Modifiers != ModifierKeys.None || e.Key is not (Key.Up or Key.Down)) return;
            Step(e.Key == Key.Up ? 1 : -1);
            e.Handled = true;
        };

        var close = FooterButton("닫기", Close);
        close.IsCancel = true; close.Background = EditorModalDesign.Color("#FBFBFB"); close.BorderBrush = EditorModalDesign.Color("#D1D1D1");
        FooterButton("이동", async () =>
        {
            try
            {
                Result.Text = await navigate(braille.IsChecked == true ? NavigationTarget.BraillePage : NavigationTarget.OriginalPage, Number.Text);
                Result.Foreground = EditorModalDesign.Color("#5F5F5F");
            }
            catch (Exception ex)
            {
                Result.Foreground = Brushes.Firebrick;
                Result.Text = ex.Message;
            }
            Result.ToolTip = Result.Text;
        }, true);
        // Use the space to the left of the footer buttons for feedback after navigation.
        var footer = (Border)Actions.Parent;
        footer.Child = null;
        var contents = new DockPanel { Margin = new(24, 0, 24, 0), Height = 64 };
        Actions.Margin = new(0); Actions.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(Actions, Dock.Right); contents.Children.Add(Actions);
        AutomationProperties.SetLiveSetting(Result, AutomationLiveSetting.Polite);
        contents.Children.Add(Result); footer.Child = contents;
        Loaded += (_, _) => { Number.Focus(); Number.SelectAll(); };
    }

    private void Step(int delta)
    {
        var value = Number.Text.Trim();
        if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var current)) return;
        var next = (long)current + delta;
        var relative = value.StartsWith('+') || value.StartsWith('-');
        if (next < int.MinValue || next > int.MaxValue || !relative && next < 1) return;
        Number.Text = (relative && next >= 0 ? "+" : "") + next.ToString(CultureInfo.InvariantCulture);
        Number.CaretIndex = Number.Text.Length;
    }
}
