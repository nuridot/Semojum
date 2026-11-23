using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Semojum.Core;

namespace Semojum.Desktop.Dialogs;

public sealed class FindDialog : FormDialogBase
{
    public FindDialog(SearchPanel target, string initialQuery, bool showReplacement,
        Func<string, SearchPanel, Task<string>> findNext, Func<string, string, SearchPanel, bool, Task<string>> replaceMatches,
        string sixDotKeys = "F D S J K L")
        : base("찾기 · 바꾸기", 520, 262, EditorModalDesign.Icon("table-cancel", assemblyName: "Semojum.Feature.Editor.Wpf"))
    {
        AlignLabelBaseline();
        var query = new TextBox { Text = initialQuery };
        var replacement = new TextBox();
        var queryEntry = Entry(query, "찾을 내용");
        var replacementEntry = (Grid)Entry(replacement, "바꿀 내용", "바꿀 말");
        var hint = replacementEntry.Children.OfType<TextBlock>().Single();
        AutomationProperties.SetAutomationId(hint, "ReplacementPlaceholder");
        var result = new TextBlock
        {
            FontSize = 12,
            LineHeight = 16,
            Margin = new(0, 16, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = EditorModalDesign.Color("#5F5F5F")
        };
        AutomationProperties.SetLiveSetting(result, AutomationLiveSetting.Polite);
        var brailleFont = new FontFamily(new Uri("pack://application:,,,/"),
            "./Semojum.Feature.Editor.Wpf;component/Assets/Fonts/#Nuridot Braille Plain, " + ((FontFamily)FindResource("InterfaceFont")).Source);
        var sixDot = new SixDotInput();
        sixDot.Configure(sixDotKeys);
        var toggle = new ToggleButton
        {
            IsChecked = target == SearchPanel.Braille,
            Width = 32,
            Height = 32,
            MinHeight = 0,
            Padding = new(0),
            Margin = new(12, 0, 0, 0),
            BorderThickness = new(1),
            Template = EditorModalDesign.ButtonTemplate(typeof(ToggleButton))
        };
        AutomationProperties.SetName(toggle, "점자 입력");

        var form = new StackPanel();
        Fields.Children.Add(Card(form, new(16, 12, 16, 12)));
        var searchRow = new DockPanel();
        DockPanel.SetDock(toggle, Dock.Right);
        searchRow.Children.Add(toggle);
        searchRow.Children.Add(queryEntry);
        FormRow(form, "찾을 내용", searchRow, 108);
        FormRow(form, "바꿀 내용", replacementEntry, 108);
        Fields.Children.Add(result);
        void UpdateToggle()
        {
            var on = toggle.IsChecked == true;
            toggle.Content = EditorModalDesign.Icon(on ? "find-braille-on" : "find-braille-off", assemblyName: "Semojum.Feature.Editor.Wpf");
            toggle.BorderBrush = EditorModalDesign.Color(on ? "#005FB8" : "#EAEAEA");
            toggle.Background = EditorModalDesign.Color(on ? "#EFF6FC" : "#FBFBFB");
            toggle.ToolTip = on ? "점자 입력 켬" : "점자 입력 끔";
            hint.Text = on ? "점자로 바꿀 점형" : "바꿀 말";
            foreach (var box in new[] { query, replacement })
            {
                box.FontSize = on ? 22 : 14;
                InputMethod.SetIsInputMethodEnabled(box, !on);
                if (on) box.FontFamily = brailleFont;
                else box.ClearValue(FontFamilyProperty);
            }
            sixDot.Reset();
            result.Text = "";
        }
        toggle.Checked += (_, _) => UpdateToggle();
        toggle.Unchecked += (_, _) => UpdateToggle();
        UpdateToggle();
        query.TextChanged += (_, _) => result.Text = "";
        SearchPanel Target() => toggle.IsChecked == true ? SearchPanel.Braille : SearchPanel.Text;

        void EnableBrailleInput(TextBox control)
        {
            control.PreviewKeyDown += (_, e) =>
            {
                if (toggle.IsChecked != true || (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) != 0) return;
                var key = Controls.SixDotKey.FromEvent(e); if (key is { } c && sixDot.Press(c)) e.Handled = true;
            };
            control.PreviewKeyUp += (_, e) =>
            {
                if (toggle.IsChecked != true) return;
                var key = Controls.SixDotKey.FromEvent(e);
                if (key is not { } c || !sixDot.IsMapped(c)) return;
                var cell = sixDot.Release(c);
                if (cell is not null)
                {
                    var start = control.SelectionStart;
                    control.SelectedText = cell;
                    control.Select(start + cell.Length, 0);
                }
                e.Handled = true;
            };
            control.PreviewTextInput += (_, e) =>
            {
                if (toggle.IsChecked == true && !DocumentSearch.IsBrailleText(e.Text)) e.Handled = true;
            };
            DataObject.AddPastingHandler(control, (_, e) =>
            {
                if (toggle.IsChecked == true && (!e.DataObject.GetDataPresent(DataFormats.UnicodeText)
                    || e.DataObject.GetData(DataFormats.UnicodeText) is not string text || !DocumentSearch.IsBrailleText(text)))
                    e.CancelCommand();
            });
            control.LostKeyboardFocus += (_, _) => sixDot.Reset();
        }
        EnableBrailleInput(query);
        EnableBrailleInput(replacement);

        async Task SearchAsync(Func<Task<string>> action)
        {
            try { result.Text = await action(); }
            catch (Exception ex) { result.Text = ex.Message; }
        }
        var close = FooterButton("닫기", Close);
        close.IsCancel = true;
        FooterButton("모두 바꾸기", () => SearchAsync(() => replaceMatches(query.Text, replacement.Text, Target(), true)));
        FooterButton("바꾸기", () => SearchAsync(() => replaceMatches(query.Text, replacement.Text, Target(), false)));
        FooterButton("다음 찾기", () => SearchAsync(() => findNext(query.Text, Target())), true);
        foreach (var button in Actions.Children.OfType<Button>().Where(button => !button.IsDefault))
        {
            button.Background = EditorModalDesign.Color("#FBFBFB");
            button.BorderBrush = EditorModalDesign.Color("#D1D1D1");
        }
        PreviewKeyDown += async (_, e) =>
        {
            if (e.Key != Key.F3) return;
            e.Handled = true;
            await RunAsync(() => SearchAsync(() => findNext(query.Text, Target())));
        };
        Loaded += (_, _) =>
        {
            if (showReplacement && query.Text.Length > 0) replacement.Focus();
            else { query.Focus(); query.SelectAll(); }
        };
    }
}
