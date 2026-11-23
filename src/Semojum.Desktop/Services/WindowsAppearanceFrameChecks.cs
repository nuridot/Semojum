using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Semojum.Desktop.Dialogs;

namespace Semojum.Desktop.Services;

internal static class WindowsAppearanceFrameChecks
{
    internal static async Task RunAsync(string directory, Action<bool,string> check)
    {
        var folder = Directory.CreateDirectory(Path.Combine(directory, "editor-appearance")).FullName;
        var window = new MainWindow(smoke: true, testWithoutAccount: true, startEmpty: true);
        window.Show();
        try
        {
            await WindowsSmoke.WithModal<AppearanceDialog>(() => window.ExecuteAsync("appearance"), async dialog =>
            {
                var tabs = Elements<TabControl>(dialog).Single();
                var font = Field<ComboBox>(dialog, "글꼴");
                var space = Field<TextBox>(dialog, "줄 간격");
                check(tabs.Items.Count == 2 && tabs.SelectedIndex == 0 && font.IsEditable && font.Text == "Pretendard"
                    && space.Text == "1.3", "Paragraph opens through the editor with the Figma font, decimal spacing and two tabs");
                Bounds(dialog, font, 203, 122, 316, 32);
                Bounds(dialog, Field<TextBox>(dialog, "텍스트 크기"), 203, 166, 96, 32);
                Bounds(dialog, Button(dialog, "기본값"), 25, 550, 88, 32);
                Bounds(dialog, Button(dialog, "확인"), 447, 550, 88, 32);
                check(dialog.ActualWidth == 560 && dialog.ActualHeight == 599, "The paragraph appearance frame is 560x599");
                await WindowsSmoke.RenderAsync(dialog, Path.Combine(folder, "paragraph.png"));
                Press(Field<RepeatButton>(dialog, "줄 간격 늘리기"));
                check(space.Text == "1.4", "Line spacing advances in tenths");
                var editable = (TextBox?)font.Template.FindName("PART_EditableTextBox", font);
                check(editable is { IsVisible:true, Text:"Pretendard" }, "The editable font name is actually painted in the combo box");
                font.IsDropDownOpen = true;
                dialog.UpdateLayout();
                check(font.Items.Contains("Arial") && font.IsDropDownOpen, "The font picker opens with installed fonts");
                font.SelectedItem = "Arial";
                font.IsDropDownOpen = false;
                check(editable?.Text == "Arial", "Choosing a listed font updates the editable field");
                editable!.Text = "Arial Custom";
                check(font.Text == "Arial Custom", "Typing a font name also updates the selection text");
                editable.Text = "Arial";
                tabs.SelectedIndex = 1;
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                dialog.UpdateLayout();
                var palette = Elements<TextBox>(dialog).Where(t => !string.IsNullOrEmpty(AutomationProperties.GetName(t))).ToArray();
                string[] expected = ["#1A1A1A","#FFFFFF","#1A1A1A","#CCE4F7","#1A1A1A","#F2F6FB","#005FB8","#8A8A8A","#FAFAFA","#005FB8","#C42B1C"];
                check(palette.Select(t => t.Text).SequenceEqual(expected), "Every fresh palette value matches the eleven original Figma swatches");
                Bounds(dialog, Field<TextBox>(dialog, "글자"), 239, 122, 240, 32);
                Bounds(dialog, Button(dialog, "확인"), 447, 662, 88, 32);
                check(dialog.ActualWidth == 560 && dialog.ActualHeight == 711, "Switching to colors fits the 560x711 frame");
                await WindowsSmoke.RenderAsync(dialog, Path.Combine(folder, "colors.png"));
                var color = Field<TextBox>(dialog, "글자");
                color.Text = "#wrong";
                Press(Button(dialog, "확인"));
                check(dialog.IsVisible && !HasForeground(0x12, 0x34, 0x56), "An invalid hex color keeps the dialog open");
                color.Text = "#123456";
                Press(Button(dialog, "적용"));
                check(dialog.IsVisible && HasForeground(0x12, 0x34, 0x56) && dialog.Result.FontFamily == "Arial" && dialog.Result.LineSpacing == 1.4,
                    "Apply previews both tabs, including the chosen font and changed swatch");
                tabs.SelectedIndex = 0;
                dialog.UpdateLayout();
                check(font.Text == "Arial" && space.Text == "1.4", "Switching back preserves staged appearance values");
                Press(Button(dialog, "취소"));
            });
            check(HasForeground(0x1a, 0x1a, 0x1a), "Cancel after Apply restores the original live editor foreground");
            await WindowsSmoke.WithModal<AppearanceDialog>(() => window.ExecuteAsync("readingOptions"), dialog =>
            {
                Field<CheckBox>(dialog, "커서 위치 점형 읽기").IsChecked = true;
                Field<TextBox>(dialog, "음성 속도").Text = "-3";
                Field<CheckBox>(dialog, "도구 모음 보기").IsChecked = false;
                Press(Button(dialog, "확인"));
                return Task.CompletedTask;
            });
            await WindowsSmoke.WithModal<AppearanceDialog>(() => window.ExecuteAsync("colors"), dialog =>
            {
                check(Elements<TabControl>(dialog).Single().SelectedIndex == 1 && Field<TextBox>(dialog, "글자").Text == "#1A1A1A",
                    "The editor color command opens the correct tab with the restored values");
                Field<TextBox>(dialog, "점역 코드").Text = "#234567";
                Press(Button(dialog, "확인"));
                check(dialog.Result is { SpeakDots:true, SpeechRate:-3, ShowToolbar:false, TagColor:"#234567" },
                    "Saving the colors page retains speech speed, dot reading and toolbar choices");
                return Task.CompletedTask;
            });
        }
        finally { await window.ReleaseResourcesAsync(); window.PrepareSmokeShutdown(); if (window.IsVisible) window.Close(); }

        bool HasForeground(byte r, byte g, byte b) => window.PageEditor.Editor.Foreground is SolidColorBrush brush && brush.Color == Color.FromRgb(r,g,b);
        void Bounds(Window owner, FrameworkElement control, int x, int y, int width, int height)
        {
            var origin = control.TranslatePoint(new(), owner);
            check(Math.Abs(origin.X-x) <= 1 && Math.Abs(origin.Y-y) <= 1 && Math.Abs(control.ActualWidth-width) <= 1 && Math.Abs(control.ActualHeight-height) <= 1,
                $"Appearance control at {origin.X},{origin.Y} {control.ActualWidth}x{control.ActualHeight} matches {x},{y} {width}x{height}");
        }
    }

    private static Button Button(Window owner, string caption) => Elements<Button>(owner).Single(b => Equals(b.Content, caption));
    private static void Press(ButtonBase button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    private static T Field<T>(Window owner, string name) where T:DependencyObject => Elements<T>(owner).Single(e => AutomationProperties.GetName(e) == name);
    private static IEnumerable<T> Elements<T>(DependencyObject root) where T:DependencyObject
    {
        for (var i=0; i<VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child=VisualTreeHelper.GetChild(root,i);
            if (child is T value) yield return value;
            foreach (var descendant in Elements<T>(child)) yield return descendant;
        }
    }
}
