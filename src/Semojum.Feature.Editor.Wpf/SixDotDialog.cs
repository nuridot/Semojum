using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Semojum.Core;

namespace Semojum.Desktop.Dialogs;

public sealed class SixDotDialog : FormDialogBase
{
    private static readonly string[] Presets = ["REWUIO", "FDSJKL", "VCXNM,"];
    private readonly RadioButton[] modes = new RadioButton[4];
    private readonly TextBlock[] handLabels = new TextBlock[2];
    private bool applyingPreset;
    // Storage and braille bit positions always use dot order; only placement follows the keyboard.
    internal TextBox[] KeyFields { get; } = new TextBox[6];
    public string Keys { get; private set; }
    public bool Space { get; }
    public bool Enter { get; }
    public bool UseCustomKeys => modes[3].IsChecked == true;

    public SixDotDialog(string current, bool allowSpace, bool allowEnter, bool useCustomKeys = false)
        : base("6점자 키설정", 640, 326, EditorModalDesign.Icon("table-cancel", assemblyName: "Semojum.Feature.Editor.Wpf"))
    {
        Keys = current; Space = allowSpace; Enter = allowEnter;
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/Semojum.UI.Common;component/Controls/CompactRadio.xaml", UriKind.Relative) });

        AlignLabelBaseline();

        var choices = new WrapPanel();
        Section("입력 방식").Children.Add(choices);
        var labels = new[] { "1단 (R E W U I O)", "2단 (F D S J K L)", "3단 (V C X N M ,)", "사용자" };
        var widths = new[] { 126d, 125d, 129d, 0d };
        for (var i = 0; i < modes.Length; i++)
        {
            var index = i;
            var radio = new RadioButton
            {
                Content = new TextBlock { Text = labels[i] },
                MinWidth = widths[i],
                Height = 20,
                Margin = new(0, 0, i < 3 ? 16 : 0, 0)
            };
            AutomationProperties.SetName(radio, labels[i]);
            modes[i] = radio;
            radio.Checked += (_, _) => SetMode(index);
            choices.Children.Add(radio);
        }

        var keys = new WrapPanel();
        Section("키 지정").Children.Add(keys);
        var chars = string.Concat(current.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
        AddHand(0, "왼손 3 · 2 · 1", [3, 2, 1]);
        AddHand(1, "오른손 4 · 5 · 6", [4, 5, 6]);
        var preset = Array.IndexOf(Presets, chars);
        modes[useCustomKeys || preset < 0 ? 3 : preset].IsChecked = true;

        CancelButton();
        var cancel = Actions.Children.OfType<Button>().Single();
        cancel.Background = EditorModalDesign.Color("#FBFBFB"); cancel.BorderBrush = EditorModalDesign.Color("#D1D1D1");
        FooterButton("확인", () =>
        {
            var value = string.Concat(KeyFields.Select(k => k.Text)).ToUpperInvariant();
            new SixDotInput().Configure(value);
            Keys = string.Join(" ", value.ToCharArray());
            DialogResult = true;
            return Task.CompletedTask;
        }, true);

        void AddHand(int handIndex, string name, int[] dots)
        {
            var hand = new StackPanel { Margin = new(0, 0, handIndex == 0 ? 32 : 0, 0) };
            var label = new TextBlock { Text = name, Height = 16, FontSize = 12, LineHeight = 16, Margin = new(0, 0, 0, 8) };
            handLabels[handIndex] = label; hand.Children.Add(label);
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var dot in dots)
            {
                var field = KeyField(dot, chars.Length >= dot ? chars[dot - 1].ToString() : "");
                field.Margin = new(0, 0, dot == dots[^1] ? 0 : 4, 0);
                KeyFields[dot - 1] = field; row.Children.Add(field);
            }
            hand.Children.Add(row); keys.Children.Add(hand);
        }
    }

    private TextBox KeyField(int dot, string value)
    {
        var field = new TextBox
        {
            Text = value,
            MaxLength = 1,
            CharacterCasing = CharacterCasing.Upper,
            Width = 44,
            Height = 32,
            MinHeight = 0,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new(0),
            BorderThickness = new(1),
            Template = (ControlTemplate)FindResource("LayoutEntry")
        };
        AutomationProperties.SetName(field, $"{dot}점 키");
        field.TextChanged += (_, _) => { if (!applyingPreset) modes[3].IsChecked = true; };
        field.GotKeyboardFocus += (_, _) => field.SelectAll();
        field.PreviewMouseLeftButtonDown += (_, e) => { if (!field.IsReadOnly) { field.Focus(); field.SelectAll(); e.Handled = true; } };
        field.PreviewTextInput += (_, e) => { if (!PrintableKey(e.Text)) e.Handled = true; };
        DataObject.AddPastingHandler(field, (_, e) =>
        {
            if (!e.DataObject.GetDataPresent(DataFormats.UnicodeText) || e.DataObject.GetData(DataFormats.UnicodeText) is not string text || !PrintableKey(text))
                e.CancelCommand();
        });
        return field;
    }

    private static bool PrintableKey(string text) => text.Length == 1 && text[0] is >= '!' and <= '~';

    private void SetMode(int index)
    {
        var custom = index == 3;
        applyingPreset = true;
        try
        {
            foreach (var label in handLabels) label.Foreground = EditorModalDesign.Color(custom ? "#5F5F5F" : "#9F9F9F");
            for (var i = 0; i < KeyFields.Length; i++)
            {
                var field = KeyFields[i];
                if (!custom) field.Text = Presets[index][i].ToString();
                field.IsReadOnly = !custom; field.Focusable = custom;
                field.Background = EditorModalDesign.Color(custom ? "#FFFFFF" : "#F5F5F5");
                field.BorderBrush = EditorModalDesign.Color(custom ? "#D1D1D1" : "#EAEAEA");
                field.Foreground = EditorModalDesign.Color(custom ? "#1A1A1A" : "#9F9F9F");
            }
        }
        finally { applyingPreset = false; }
    }
}
