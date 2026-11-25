using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Semojum.Core;
using Semojum.Desktop.Dialogs;

namespace Semojum.Desktop.Services;

internal static class WindowsRibbonWorkflowChecks
{
    private static IEnumerable<T> Children<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var item = VisualTreeHelper.GetChild(parent, i);
            if (item is T child) yield return child;
            foreach (var descendant in Children<T>(item)) yield return descendant;
        }
    }

    internal static async Task RunAsync(Action<bool,string> check)
    {
        await WindowsEditorFrameChecks.RunAsync(check);
        var window = new MainWindow(smoke: true, testWithoutAccount: true, startEmpty: true) { Width = 1280, Height = 800 };
        window.Show();
        try
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var tabs = window.EditorRibbon.Items.Cast<TabItem>().ToArray();
            check(tabs.Select(t => t.Header).SequenceEqual(["파일","홈","삽입","점역","보기","도움말"]),
                "The six editor ribbon tabs appear in design order");
            check(window.EditorRibbon.SelectedItem == tabs[1] && tabs[1].ActualHeight == 32 && window.RuleSearch.Width == 200,
                "The editor starts on Home with its 32px tab and 200px rule search");
            double HeaderX(FrameworkElement element) => element.TranslatePoint(new Point(), window.RibbonHeaderActions).X;
            check(HeaderX(window.RuleSearch) < HeaderX(window.ToolbarAccount) && HeaderX(window.ToolbarAccount) < HeaderX(window.RibbonNotifications),
                "The ribbon header places search, account, and notifications in design order");
            check(Children<TextBlock>(window.SourceHeader).Any(t => t.Text == "묵자"), "The source panel uses the Figma C1/C3 묵자 heading");
            window.EditorRibbon.SelectedIndex = 4;
            window.UpdateLayout();
            var groupShortcut = window.AdditionalCommands;
            var toggle = new System.Windows.Automation.Peers.ToggleButtonAutomationPeer(groupShortcut);
            ((System.Windows.Automation.Provider.IToggleProvider)toggle).Toggle();
            await Dispatcher.Yield(DispatcherPriority.DataBind);
            var ribbonMenu = window.EditorRibbon.ContextMenu!;
            ribbonMenu.PlacementTarget = window.EditorRibbon;
            ribbonMenu.IsOpen = true;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var menuItem = ribbonMenu.Items.Cast<MenuItem>().Single(item => Equals(item.Header, "전체 명령"));
            check(groupShortcut.IsVisible && groupShortcut.Focusable && groupShortcut.ActualHeight == 16 &&
                  AutomationProperties.GetName(groupShortcut) == "편집창 전체 명령" && window.ApplicationMenu.IsVisible && menuItem.IsChecked,
                  "The View group caption opens all commands and exposes the same state in the ribbon context menu");
            var peer = new System.Windows.Automation.Peers.MenuItemAutomationPeer(menuItem);
            ((System.Windows.Automation.Provider.IToggleProvider)peer).Toggle();
            ribbonMenu.IsOpen = false;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            check(groupShortcut.IsChecked == false && !window.ApplicationMenu.IsVisible,
                "The native ribbon menu closes all commands and resets the group caption");
            window.EditorRibbon.SelectedIndex = 1;
            var captures = Environment.GetEnvironmentVariable("NURIDOT_QA_CAPTURE_DIR");
            foreach (var size in new[] { (Width: 1440, Height: 1000), (Width: 1280, Height: 800) })
            {
                window.WindowState = WindowState.Normal;
                window.Width = size.Width;
                window.Height = size.Height;
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                foreach (var tab in tabs)
                {
                    window.EditorRibbon.SelectedItem = tab; window.UpdateLayout();
                    check(tab.IsSelected && Children<FrameworkElement>(tab.Content as ScrollViewer ?? throw new InvalidOperationException())
                        .OfType<Button>().Any(b => b.IsVisible && b.ActualWidth > 0 && AutomationProperties.GetName(b).Length > 0),
                        $"Ribbon tab {tab.Header} exposes a visible named command at {size.Width}×{size.Height}");
                    if (!string.IsNullOrWhiteSpace(captures))
                        await WindowsSmoke.RenderAsync(window, System.IO.Path.Combine(captures, $"ribbon-{size.Width}-{tab.Header}.png"));
                }
            }
            window.EditorRibbon.SelectedIndex = 0; window.UpdateLayout();
            var create = Children<Button>(window.EditorRibbon).Single(b => Equals(b.Tag, "new"));
            create.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
            check(!window.OriginalPanel.IsVisible && window.SourcePanel.IsVisible && window.BrailleContainer.IsVisible,
                "The ribbon New command opens a two-panel text document");
            window.HidePanel("hideSource"); window.HidePanel("hideBraille"); window.UpdateLayout();
            check(window.EmptyPanelActions.IsVisible, "Closing all panels exposes the recovery actions");
            Children<Button>(window.EmptyPanelActions).Single(b => Equals(b.Tag,"all")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.UpdateLayout();
            check(window.OriginalPanel.IsVisible && window.SourcePanel.IsVisible && window.BrailleContainer.IsVisible && !window.EmptyPanelActions.IsVisible,
                "The restore-all action reopens all three panels");
            var available = new HashSet<int> { 3, 5, 6, 7 };
            check(ReconversionPages.Parse("7, 3, 5–7", available).SequenceEqual([3,5,6,7]), "Reconversion de-duplicates and orders selected completed pages");
            foreach (var invalid in new[] { "3-5", "7-5", "5,", "0", "5-2147483647" })
            {
                bool rejected = false;
                try { ReconversionPages.Parse(invalid, available); } catch (ArgumentException) { rejected = true; }
                check(rejected, $"Reconversion rejects unavailable or invalid pages ({invalid})");
            }
            var preview = new DocumentPreviewWindow([new(3,"셋째 쪽"),new(5,"다섯째 쪽")], [["⠁"],["⠃"]], "fixture", false,
                3, 2, true, "입력 한글", false, false, _ => { }) { Owner = window };
            preview.Show();
            try
            {
                preview.UpdateLayout();
                check(preview.IsSource && Children<RichTextBox>(preview).Any(b => b.IsVisible && new System.Windows.Documents.TextRange(b.Document.ContentStart,b.Document.ContentEnd).Text.Contains("셋째 쪽")),
                    "Source print preview displays the original source text");
                Children<Button>(preview).Single(b => AutomationProperties.GetName(b) == "점자 페이지").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check(!preview.IsSource && preview.Face == 2, "The braille preview retains its independent page");
            }
            finally { preview.Close(); }
            var originalOnly = new DocumentPreviewWindow([new(1, "점역 전 원문")], [], "fixture", false, 1, 1, true, "입력 한글", false, false,
                _ => { }, "점역할 내용이 없습니다.") { Owner = window };
            originalOnly.Show();
            try { check(originalOnly.IsSource && !Children<Button>(originalOnly).Single(b=>AutomationProperties.GetName(b)=="점자 페이지").IsEnabled,
                    "An untranslated document opens its source preview with the braille tab disabled"); }
            finally { originalOnly.Close(); }
            CheckReconversionDialog(window, check);
            await CheckRibbonChoicesAsync(window, check);
        }
        finally { await window.ReleaseResourcesAsync(); window.PrepareSmokeShutdown(); window.Close(); }
    }

    private static async Task CheckRibbonChoicesAsync(MainWindow window, Action<bool, string> check)
    {
        Button Find(string title) => Children<Button>(window.EditorRibbon).Single(b => AutomationProperties.GetName(b) == title);
        ContextMenu Open(Button button)
        {
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var menu = button.ContextMenu ?? throw new InvalidOperationException("Missing menu for " + AutomationProperties.GetName(button));
            menu.UpdateLayout();
            return menu;
        }
        var owner = window.Session;
        var ownerTab = window.DocumentTabs.SelectedItem;
        (string Title, int Count, string? Tag)[] groups = [
            ("정렬",3,"가운데정렬"), ("강조",4,"굵은"), ("빈칸",2,"네모빈칸"), ("글상자",3,"상자2"), ("점자 줄",5,"중단줄"), ("표 테두리",7,"표중앙"),
            ("한글",2,null), ("영어",2,null), ("줄바꿈 단위",2,null)
        ];
        foreach (var (title, count, tag) in groups)
        {
            window.EditorRibbon.SelectedIndex = tag is null ? 3 : 2; window.UpdateLayout();
            var button = Find(title);
            var menu = Open(button);
            var items = menu.Items.Cast<MenuItem>().ToArray();
            var card = (Border)menu.Template.FindName("Card", menu);
            check(menu.IsOpen && items.Length == count && card.ActualWidth == 240 && card.ActualHeight == count * 32 + 10
                && items.All(i => i.ActualHeight == 32), $"{title} has the reference 240px card and {count} native 32px rows");
            var initial = items.Single(i => i.IsChecked);
            check(Equals(initial.Header, button.Content), $"{title} marks the selection displayed on the ribbon");
            var choice = tag is not null || title == "줄바꿈 단위" ? items[1] : items[0];
            var before = owner.ActiveBlock.Source;
            var editor = window.PageEditor.Visibility == Visibility.Visible ? window.PageEditor.Editor : window.SourceEditor;
            editor.Select(editor.Text.Length, 0);
            choice.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, choice));
            menu.IsOpen = false; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            bool applied = tag is not null ? owner.ActiveBlock.Source == before + BrailleTags.Get(tag).Insert("")
                : title switch { "한글" => owner.Options.KoreanGrade1, "영어" => owner.Options.EnglishGrade1, _ => owner.Options.LineBreakUnit == "word" };
            var reopened = Open(button);
            check(applied && Equals(button.Content, choice.Header) && Equals(reopened.Items.Cast<MenuItem>().Single(i => i.IsChecked).Header, choice.Header),
                $"{title} applies the real document edit or translation option and remembers it on reopen");
            reopened.IsOpen = false; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        }
        var layout = owner.Layout;
        Find("칸 늘리기").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Find("줄 줄이기").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(owner.Layout.Columns == layout.Columns + 1 && owner.Layout.Lines == layout.Lines - 1, "Both ribbon steppers update the document layout");

        window.EditorRibbon.SelectedIndex = 2; window.UpdateLayout();
        var align = Find("정렬");
        var pending = Open(align);
        var stale = (MenuItem)pending.Items[2];
        var source = owner.ActiveBlock.Source;
        owner.IsReadOnly = true;
        stale.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        check(!align.IsEnabled && !pending.IsOpen && owner.ActiveBlock.Source == source, "A read-only transition closes an open ribbon menu and rejects its stale edit");
        owner.IsReadOnly = false;
        pending = Open(align); stale = (MenuItem)pending.Items[2];
        await window.ExecuteAsync("new"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var next = window.Session;
        var nextSource = next.ActiveBlock.Source;
        stale.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        check(next != owner && !pending.IsOpen && Equals(align.Content, "왼쪽") && next.ActiveBlock.Source == nextSource && owner.ActiveBlock.Source == source,
            "Another document starts with its own tag selection and cannot receive the previous document's open menu command");
        window.DocumentTabs.SelectedItem = ownerTab; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        check(window.Session == owner && Equals(align.Content, "가운데"), "Switching back restores the tag selection for that document");
    }

    private static void CheckReconversionDialog(Window owner, Action<bool, string> check)
    {
        var dialog = new PageReconversionDialog(3, new HashSet<int> { 3, 5 }, page => page == 3) { Owner = owner };
        Exception? failure = null;
        var prompts = new List<string>();
        void Answer(int choice)
        {
            dialog.Dispatcher.BeginInvoke(new Action(() =>
            {
                var warning = Application.Current.Windows.OfType<NoticeDialog>().SingleOrDefault(w => w.Owner == dialog && w.IsVisible);
                if (warning is null) return;
                prompts.Add(warning.DetailText);
                warning.ActionButtons[choice].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }), DispatcherPriority.ApplicationIdle);
        }
        dialog.ContentRendered += (_, _) => dialog.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                Children<RadioButton>(dialog).Single(r => Equals(r.Content, "쪽 지정")).IsChecked = true;
                Children<TextBox>(dialog).Single(t => t.ToolTip is string).Text = "3, 5";
                Children<RadioButton>(dialog).Single(r => AutomationProperties.GetName(r) == "중급").IsChecked = true;
                var submit = Children<Button>(dialog).Single(b => Equals(b.Content, "재변환"));
                Answer(1);
                submit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check(dialog.Selection is null && dialog.IsVisible, "Canceling the overwrite returns to reconversion without creating a job");
                Answer(0);
                submit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            catch (Exception ex) { failure = ex; }
            finally { if (dialog.IsVisible) dialog.Close(); }
        }), DispatcherPriority.ApplicationIdle);
        var accepted = dialog.ShowDialog();
        if (failure is not null) throw new InvalidOperationException("Reconversion interaction failed", failure);
        check(accepted == true && dialog.Selection is { ProofModel: "HAIKU", Pages: [3, 5] },
            "Explicit overwrite confirms the chosen pages and model for the current job");
        check(prompts.Count == 2 && prompts.All(p => p.StartsWith("3쪽에", StringComparison.Ordinal)),
            "Reconversion warns only about the manually edited page in the selection");
    }
}
