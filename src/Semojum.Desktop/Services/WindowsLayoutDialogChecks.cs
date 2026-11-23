using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Semojum.Core;
using Semojum.Desktop.Dialogs;
using SharpVectors.Converters;

namespace Semojum.Desktop.Services;

internal static class WindowsLayoutDialogChecks
{
    internal static async Task RunAsync(string directory, Action<bool, string> check)
    {
        var editor = new MainWindow(smoke: true);
        editor.Show();
        try
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            editor.Session.Layout = new PageLayout(EditScope: "page", PadToEven: true);
            var initial = editor.Session.Layout;
            await Open(async dialog =>
            {
                await WindowsSmoke.RenderAsync(dialog, Path.Combine(directory, "page-layout.png"));
                var boxes = Descendants(dialog).OfType<TextBox>().ToArray();
                var toggles = Descendants(dialog).OfType<CheckBox>().ToArray();
                var radios = Descendants(dialog).OfType<RadioButton>().ToArray();
                check(dialog.ActualWidth == 560 && dialog.ActualHeight == 738 && boxes.Length == 6 && toggles.Length == 3 && radios.Length == 11,
                    "Layout has the 560×738 Figma frame and every visible field");
                var cardBounds = Descendants(dialog).OfType<Border>().Where(b => b.Child is StackPanel && b.CornerRadius.TopLeft == 8)
                    .Select(b => b.TransformToAncestor(dialog).TransformBounds(new Rect(b.RenderSize))).ToArray();
                check(cardBounds.Length == 3 && cardBounds.Zip(new[] { new Rect(25,85,510,232), new Rect(25,361,510,152), new Rect(25,557,510,108) })
                    .All(pair => Close(pair.First, pair.Second)), "All three layout cards match their Figma bounds: " + string.Join(" / ", cardBounds.AsEnumerable()));
                foreach (var (name,count,width,height) in new[] { ("table-cancel.svg",1,16d,16d), ("layout-step-up.svg",5,7.20047,3.89951), ("layout-step-down.svg",5,12d,12d) })
                {
                    var icons = Descendants(dialog).OfType<SvgViewbox>().Where(i => i.Source?.ToString().EndsWith(name, StringComparison.Ordinal) == true).ToArray();
                    check(icons.Length == count && icons.All(i => i.Drawings is { Bounds.IsEmpty: false } && Math.Abs(i.Width-width)<0.01 && Math.Abs(i.Height-height)<0.01),
                        name + " renders from the original Figma asset in every layout slot");
                }
                var hint = Descendants(dialog).OfType<TextBlock>().Single(t => t.Text == "1-5, 8, 10-");
                check(hint.IsVisible && Box(dialog,"번호 범위").Text == "" && toggles.All(t => t.IsChecked == true), "Layout defaults show an empty range hint and all three switches on");
                Box(dialog,"페이지당 줄 수").Text = "3";
                Click(dialog,"확인");
                check(dialog.IsVisible && editor.Session.Layout == initial && Descendants(dialog).OfType<TextBlock>().Any(t => t.IsVisible && t.Text.Contains("4–100")),
                    "Invalid page dimensions leave the dialog open and the document untouched");
                Box(dialog,"페이지당 줄 수").Text = "31";
                ClickNamed(dialog,"페이지당 줄 수 늘리기");
                Box(dialog,"줄당 칸 수").Text = "42";
                Box(dialog,"점자 페이지 시작 번호").Text = "11";
                Box(dialog,"원본 쪽 시작 번호").Text = "6";
                ClickNamed(dialog,"표지 쪽 수 줄이기");
                check(Box(dialog,"표지 쪽 수").Text == "0", "Cover decrement respects the minimum");
                ClickNamed(dialog,"표지 쪽 수 늘리기");
                Radio(dialog, "쪽 번호 넣을 면 짝수").IsChecked = true;
                Radio(dialog, "꼬리말 넣을 면 모든").IsChecked = true;
                Radio(dialog, "꼬리말 정렬 오른쪽").IsChecked = true;
                Box(dialog,"번호 범위").Text = "1-4, 3-5"; Click(dialog,"확인");
                check(dialog.IsVisible && editor.Session.Layout == initial && !hint.IsVisible, "Overlapping ranges fail without committing pending edits or treating the hint as input");
                Box(dialog,"번호 범위").Text = "1-5, 8, 10-";
                foreach (var toggle in toggles)
                {
                    var peer = new CheckBoxAutomationPeer(toggle);
                    ((IToggleProvider)peer.GetPattern(PatternInterface.Toggle)!).Toggle();
                }
                Click(dialog,"확인");
            });
            var saved = editor.Session.Layout;
            check(saved.Columns == 42 && saved.Lines == 32 && saved.StartPage == 11 && saved.SourcePageStart == 6 && saved.CoverPages == 1
                && saved.PageNumbers == "짝수 페이지" && saved.Footer == "모든 페이지" && saved.FooterAlign == "right"
                && !saved.ShowSourcePageNumber && !saved.ShowBraillePageNumber && !saved.ShowChangeLine
                && saved.EditScope == "page" && saved.PadToEven && PageNumberRange.Format(saved.NumberRanges) == "1-5, 8, 10-",
                "The editor layout command commits every edited field and preserves unrelated document options");
            await Open(dialog =>
            {
                check(Box(dialog, "원본 쪽 시작 번호").Text == "6" && Radio(dialog, "쪽 번호 넣을 면 짝수").IsChecked == true
                    && Descendants(dialog).OfType<CheckBox>().All(c => c.IsChecked == false), "Reopening the editor command restores the saved document values");
                dialog.Height = 400; dialog.UpdateLayout();
                var scroll = Descendants(dialog).OfType<ScrollViewer>().Single(s => s.Content is StackPanel);
                scroll.ScrollToEnd(); dialog.UpdateLayout();
                check(scroll.ScrollableHeight > 0 && scroll.VerticalOffset > 0 && Action(dialog,"확인").IsVisible,
                    "The display switches are reachable by scrolling on a short monitor while the footer stays visible");
                Box(dialog, "원본 쪽 시작 번호").Text = "99"; Click(dialog, "취소"); return Task.CompletedTask;
            });
            check(editor.Session.Layout == saved, "Canceling preserves the last applied document settings");
            var path = Path.Combine(directory, "page-layout.smj");
            await SmjFile.SaveAsync(path, SmjFile.Snapshot(editor.Session));
            var restored = (await SmjFile.LoadAsync(path)).Layout;
            check(restored with { NumberRanges = null } == saved with { NumberRanges = null }
                && PageNumberRange.Format(restored.NumberRanges) == "1-5, 8, 10-",
                "A real SMJ save and reopen retains the full layout and its disjoint and open-ended ranges");
        }
        finally { editor.Session.IsDirty = false; editor.Close(); }

        async Task Open(Func<LayoutDialog, Task> inspect)
        {
            var interaction = editor.Dispatcher.InvokeAsync(async () =>
            {
                var dialog = Application.Current.Windows.OfType<LayoutDialog>().Single(w => w.Owner == editor);
                try { await inspect(dialog); }
                finally { if (dialog.IsVisible) dialog.Close(); }
            }, DispatcherPriority.ApplicationIdle);
            await editor.ExecuteAsync("layout");
            await interaction.Task.Unwrap();
        }
    }

    private static bool Close(Rect a, Rect b) => Math.Abs(a.X-b.X)<=1 && Math.Abs(a.Y-b.Y)<=1 && Math.Abs(a.Width-b.Width)<=1 && Math.Abs(a.Height-b.Height)<=1;
    private static TextBox Box(Window w, string name) => Descendants(w).OfType<TextBox>().Single(x => AutomationProperties.GetName(x) == name);
    private static RadioButton Radio(Window w, string name) => Descendants(w).OfType<RadioButton>().Single(x => AutomationProperties.GetName(x) == name);
    private static Button Action(Window w, string label) => Descendants(w).OfType<Button>().Single(x => Equals(x.Content,label));
    private static void Click(Window w,string label) => Action(w,label).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    private static void ClickNamed(Window w,string name) => Descendants(w).OfType<ButtonBase>().Single(x => AutomationProperties.GetName(x) == name).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root,i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
