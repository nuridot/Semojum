using System.Globalization;
using System.IO;
using Semojum.Core;
using Semojum.Core.Integration;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Semojum.Desktop.Services;

internal static class WindowsStatusZoomChecks
{
    internal static async Task RunAsync(string directory, Action<bool, string> check)
    {
        var window = new MainWindow(smoke: true, testWithoutAccount: true, startEmpty: true) { Width = 1440, Height = 1000 };
        window.Show();
        try
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();
            var sourceButtons = Children(window.SourcePageBar).OfType<Button>().ToArray();
            var minus = sourceButtons.Single(b => AutomationProperties.GetName(b) == "묵자 축소");
            var plus = sourceButtons.Single(b => AutomationProperties.GetName(b) == "묵자 확대");
            var percent = sourceButtons.Single(b => AutomationProperties.GetName(b) == "묵자 확대 비율");
            var braillePlus = Children(window.Braille.FacePageBar).OfType<Button>().Single(b => AutomationProperties.GetName(b) == "점자 확대");
            var braillePercent = Children(window.Braille.FacePageBar).OfType<Button>().Single(b => AutomationProperties.GetName(b) == "점자 확대 비율");
            check(Equals(percent.Content, "100%") && Equals(braillePercent.Content, "100%") && !window.PanelZoomPopup.IsOpen,
                "Each header shows its independent zoom percent before the slider opens");
            percent.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            check(window.StatusBar.ActualHeight == 28 && window.ZoomSlider.Value == 50 && window.ZoomPercent.Text == "100%" && window.ZoomSlider.ActualWidth == 106,
                "Status zoom starts at the Figma 100-percent midpoint with a 106-pixel track");
            window.PanelZoomPopup.IsOpen = false;
            await CheckStatusCellsAsync(window, check, directory);
            plus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(window.SourceEditor.FontSize == 16 && window.Braille.GridView.FontSize == 22 && Equals(percent.Content, "114%") && Equals(braillePercent.Content, "100%"),
                "Zoom-in updates only the active text panel and its displayed percentage");
            minus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(window.SourceEditor.FontSize == 14 && window.ZoomSlider.Value == 50 && window.ZoomPercent.Text == "100%",
                "Zoom-out returns to the original size and synchronizes the slider");
            window.ZoomSlider.Value = 75;
            check(window.SourceEditor.FontSize == 23 && window.ZoomPercent.Text == "164%", "Moving the slider changes the actual editor size");
            window.ZoomPicker.SelectedItem = "125%";
            check(window.SourceEditor.FontSize == 17.5 && Math.Abs(window.ZoomSlider.Value - (50 + 3.5 * 50 / 18)) < 0.001,
                "Existing zoom selection remains synchronized with the status slider");
            window.ZoomSlider.Value = 0;
            check(window.SourceEditor.FontSize == 10, "Slider retains the existing minimum font size");
            window.ZoomSlider.Value = 100;
            check(window.SourceEditor.FontSize == 32, "Slider retains the existing maximum font size");
            window.Braille.GridView.Focus();
            braillePlus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(window.Braille.GridView.FontSize == 24 && window.SourceEditor.FontSize == 32 && Equals(window.FontSizeLabel.SelectedItem, "24"),
                "Toolbar and status controls follow the focused braille grid");
            check(window.TryPanelZoomKey(Key.OemPlus, ModifierKeys.Control | ModifierKeys.Shift)
                && window.Braille.GridView.FontSize == 26 && window.SourceEditor.FontSize == 32, "Ctrl plus resizes only braille");
            check(window.TryPanelZoomKey(Key.Subtract, ModifierKeys.Control)
                && window.TryPanelZoomWheel(-120, ModifierKeys.Control) && window.Braille.GridView.FontSize == 22
                && !window.TryPanelZoomWheel(120, ModifierKeys.None), "Keypad minus and Ctrl wheel share the active size; ordinary scrolling stays available");
            window.PageEditor.Editor.Focus();
            check(Equals(window.FontSizeLabel.SelectedItem, "32"), "Returning to source restores its independent zoom reading");
            check(window.ZoomSlider.Focusable && minus.Focusable && plus.Focusable && AutomationProperties.GetName(window.ZoomSlider) == "편집기 확대 비율",
                "Zoom slider and both actions are keyboard focusable and named for assistive technology");
            window.MinWidth = 480; // as WindowBounds.Fit allows on a monitor narrower than the 1280-pixel minimum
            foreach (var width in new[] { 1280.0, 480.0 })
            {
                window.Width = width;
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
                percent.BringIntoView(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                var position = percent.TransformToAncestor(window.PanelScroll).TransformBounds(new Rect(percent.RenderSize));
                check(percent.IsVisible && position.Left >= -1 && position.Right <= window.PanelScroll.ViewportWidth + 1,
                    $"The source header zoom selector is reachable at {width:0}px window width");
            }
            if (Environment.GetEnvironmentVariable("STATUS_ZOOM_CAPTURE") is { Length: > 0 } capture)
                await WindowsSmoke.RenderElementAsync(window.StatusBar, capture);
        }
        finally { await window.ReleaseResourcesAsync(); window.PrepareSmokeShutdown(); window.Close(); }
    }

    // The B2 footer sizes each cell to its real text and separates it with a short divider.
    private static async Task CheckStatusCellsAsync(MainWindow window, Action<bool, string> check, string directory)
    {
        var parts = ((StackPanel)window.StatusInput.Parent).Children.OfType<FrameworkElement>().ToArray();
        var texts = parts.OfType<TextBlock>().ToArray(); var dividers = parts.OfType<Border>().ToArray();
        double Left(FrameworkElement element) => element.TranslatePoint(new Point(), window.StatusBar).X;
        check(texts.SequenceEqual(new[] { window.StatusInput, window.StatusPage, window.CaretStatus, window.StatusOptions })
            && Math.Abs(Left(texts[0]) - 16) <= 1 && dividers.Length == 3
            && dividers.All(d => d.ActualWidth == 1 && d.ActualHeight == 14 && d.Background is SolidColorBrush { Color: var line } && line == Color.FromRgb(234, 234, 234))
            && texts.All(t => t.FontSize == 12 && t.Margin.Left == 8 && t.Margin.Right == 8)
            && window.StatusPage.Foreground is SolidColorBrush { Color: var ink } && ink == Color.FromRgb(26, 26, 26),
            "The B2 footer places three 1x14 dividers between four content-sized 12px labels");
        check(Regex.IsMatch(window.CaretStatus.Text, @"^\d+쪽 · \d+줄 · \d+칸$") && window.StatusPage.Text == "묵자 패널"
            && Regex.IsMatch(window.StatusOptions.Text, @"^한글 [12]급 · 영어 [12]급$"),
            $"The footer reads the active pane, actual page caret and language grades ({window.StatusPage.Text} | {window.CaretStatus.Text} | {window.StatusOptions.Text})");
        await CheckJobStatesAsync(window, check, directory);


        // The first cell is the keyboard's 한/영 state rather than the focused panel.
        var korean = new CultureInfo("ko-KR");
        check(MainWindow.InputStatusText(korean, ImeConversionModeValues.Native | ImeConversionModeValues.FullShape) == "입력 한글"
            && MainWindow.InputStatusText(korean, ImeConversionModeValues.Alphanumeric) == "입력 영문"
            && MainWindow.InputStatusText(new CultureInfo("en-US"), ImeConversionModeValues.Native) == "입력 영문",
            "Hangul conversion on a Korean keyboard reads 입력 한글 and every other input reads 입력 영문");
        var languages = InputLanguageManager.Current;
        var previousLanguage = languages.CurrentInputLanguage; var previousMode = InputMethod.Current.ImeConversionMode;
        var installed = languages.AvailableInputLanguages?.OfType<CultureInfo>().FirstOrDefault(c => c.TwoLetterISOLanguageName == "ko");
        try
        {
            window.PageEditor.Editor.Focus();
            if (installed is null)
            {
                check(window.StatusInput.Text == "입력 영문", $"Without a Korean keyboard the status bar reports English input (actual {window.StatusInput.Text})");
                return;
            }
            languages.CurrentInputLanguage = installed;
            InputMethod.Current.ImeConversionMode = ImeConversionModeValues.Native;
            await WaitUntilAsync(() => window.StatusInput.Text == "입력 한글");
            check(window.StatusInput.Text == "입력 한글", $"Switching the Korean keyboard to Hangul updates the status bar (actual {window.StatusInput.Text}, mode {InputMethod.Current.ImeConversionMode})");
            InputMethod.Current.ImeConversionMode = ImeConversionModeValues.Alphanumeric;
            await WaitUntilAsync(() => window.StatusInput.Text == "입력 영문");
            check(window.StatusInput.Text == "입력 영문", $"Switching back to English with 한/영 updates the status bar (actual {window.StatusInput.Text})");
            InputMethod.Current.ImeConversionMode = ImeConversionModeValues.Native;
            await WaitUntilAsync(() => window.StatusInput.Text == "입력 한글");
            InputMethod.SetIsInputMethodEnabled(window.RuleSearch, false);
            check(window.RuleSearch.Focus(), "The rule search box takes keyboard focus");
            await WaitUntilAsync(() => window.StatusInput.Text == "입력 영문");
            check(window.StatusInput.Text == "입력 영문", $"Moving focus to a control without an IME reports English input (actual {window.StatusInput.Text})");
            window.PageEditor.Editor.Focus();
            await WaitUntilAsync(() => window.StatusInput.Text == "입력 한글");
            check(window.StatusInput.Text == "입력 한글", $"Returning focus to the text editor reports Hangul input again (actual {window.StatusInput.Text})");
        }
        finally
        {
            window.RuleSearch.ClearValue(InputMethod.IsInputMethodEnabledProperty);
            if (previousLanguage is not null) languages.CurrentInputLanguage = previousLanguage;
            InputMethod.Current.ImeConversionMode = previousMode;
        }
    }

    private sealed class WaitingEngine : ITranslationEngine
    {
        public TaskCompletionSource<string> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<string> TranslateAsync(string text, int blockId, CancellationToken ct) => Result.Task.WaitAsync(ct);
    }

    private static async Task CheckJobStatesAsync(MainWindow window, Action<bool, string> check, string directory)
    {
        var capture = Directory.CreateDirectory(Path.Combine(directory, "job-status")).FullName;
        Task Capture(string name) => WindowsSmoke.RenderElementAsync(window.StatusBar, Path.Combine(capture, name + ".png"));
        var model = window.Session;
        var block = model.ActiveBlock!;
        var previousWidth = window.Width;
        window.Width += 1438 - window.StatusBar.ActualWidth;
        window.UpdateLayout();
        model.JobId = "footer-qa";
        model.Progress = new(128, 80, 4, 1, "");
        check(window.ConversionStatus.IsVisible && window.ConversionStatusText.Text == "변환 중 84/128쪽", "The active job shows live finished and total page counts in the footer");
        await Capture("conversion");
        window.ConversionStatus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var menu = window.ConversionStatus.ContextMenu;
        check(menu is { IsOpen: true, Width: 200 } && menu.Items.OfType<MenuItem>().Single() is { Header: "변환 취소", IsEnabled: true, Height: 32, Icon: not null },
            "The active conversion opens a 200px menu with its 32px enabled cancellation action");
        await Capture("cancel");
        var surface = (Border)window.ConversionStatus.Template.FindName("Surface", window.ConversionStatus);
        check(menu.IsOpen && surface.Background is SolidColorBrush { Color: var fill } && fill == Color.FromRgb(240, 240, 240),
            $"The open cancellation menu keeps the job button highlighted (open {menu.IsOpen}, fill {surface.Background}, tag {window.ConversionStatus.Tag})");
        await WindowsSmoke.RenderElementAsync(menu, Path.Combine(capture, "cancel-menu.png"));
        var pages = Enumerable.Range(1, 12).ToDictionary(n => n, _ => "COMPLETED");
        pages[13] = "BLOCKED"; pages[14] = "RUNNING";
        var status = new JobStatus("footer-qa", 128, 13, "IN_PROGRESS", pages);
        model.Progress = status.ToProgress("", "SONNET");
        menu.IsOpen = false;
        check(window.ConversionStatusText.Text == "교열 중 12/128쪽" && !status.ToProgress("").IsProofreading,
            "A running page with a known proof model shows only successful proofread pages, while unspecified models keep conversion status");
        await Capture("proofreading");
        pages[14] = "PENDING";
        check(!status.ToProgress("", "SONNET").IsProofreading, "Queued pages waiting for a proof model do not claim active proofreading");
        window.ConversionStatus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var closingMenu = window.ConversionStatus.ContextMenu;
        model.Progress = null;
        check(!closingMenu.IsOpen && !window.ConversionStatus.IsVisible && window.BlockStatus.IsVisible,
            "An arriving terminal result closes the cancellation menu and restores the active block status");
        block.State = "교열 완료";
        await Capture("proofread");
        TextBlock Label() => (TextBlock)window.BlockStatus.Template.FindName("Label", window.BlockStatus);
        bool HasIcon(string name) => ((FrameworkElement)window.BlockStatus.Template.FindName(name, window.BlockStatus)).IsVisible;
        check(Label().Text == "교열 완료" && HasIcon("DoneIcon"), "A returned proofread block shows the completion check");
        var engine = new WaitingEngine();
        var translating = new TranslationCoordinator(engine).TranslateAsync(block, ManualEditDecision.Overwrite, TimeSpan.FromSeconds(15));
        await Capture("translation");
        check(Label().Text == "점역 중" && HasIcon("BusyIcon") && !HasIcon("DoneIcon"), "An outstanding translation shows the live busy glyph");
        engine.Result.SetResult("⠁");
        var result = await translating;
        await Capture("translated");
        check(result == TranslationOutcome.Applied && Label().Text == "점역 완료" && HasIcon("DoneIcon") && Equals(window.BlockStatus.ToolTip, "점역됨 · 검수 전"),
            "An applied translation turns green with a check and keeps its unreviewed detail in the tooltip");
        foreach (var state in new[] { "누락 문자 · 검토 필요", "점역 실패 · 점자 잠김", "직접 수정" })
        {
            block.State = state;
            check(Label().Text == state && !HasIcon("DoneIcon") && !HasIcon("BusyIcon"), $"The footer preserves the real {state} state without completion marks");
        }
        model.JobId = null;
        window.Width = previousWidth;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(25);
    }

    private static IEnumerable<DependencyObject> Children(DependencyObject parent)
    {
        yield return parent;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            foreach (var child in Children(VisualTreeHelper.GetChild(parent, i))) yield return child;
    }
}
