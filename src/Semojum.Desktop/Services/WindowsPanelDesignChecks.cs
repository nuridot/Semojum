using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SharpVectors.Converters;
using Semojum.Core;

namespace Semojum.Desktop.Services;

internal static class WindowsPanelDesignChecks
{
    internal static async Task RunAsync(string directory, Action<bool, string> check)
    {
        var window = new MainWindow(smoke: true, testWithoutAccount: true, startEmpty: true) { Width = 1440, Height = 1000 };
        window.Show();
        try
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();
            check(window.OriginalHeader.ActualHeight == 36 && window.SourceHeader.ActualHeight == 36 && window.Braille.Header.ActualHeight == 36,
                "All three Figma panel headers are 36 pixels high");
            check(window.OriginalPosition.Text == "" && window.OriginalPageBar.IsVisible && Descendants(window.OriginalPageBar).OfType<TextBox>().Single() is { IsEnabled: false, Text: "—" },
                "A blank original shows no invented page number or duplicate page bar");
            check(window.OriginalPanel.ActualWidth >= 260 && window.SourcePanel.ActualWidth >= 300 && window.Braille.ActualWidth >= 300 && window.PanelScroll.ViewportWidth > 0,
                $"Initially fitted window keeps each panel at a usable width (window={window.ActualWidth:F1}, viewport={window.PanelScroll.ViewportWidth:F1})");
            foreach (var (header, name) in new[] { (window.OriginalHeader, "원본 패널 숨기기"), (window.SourceHeader, "묵자 패널 숨기기"), (window.Braille.Header, "점자 패널 숨기기") })
            {
                var button = Descendants(header).OfType<Button>().Single(b => AutomationProperties.GetName(b) == name);
                button.BringIntoView(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
                var bounds = button.TransformToAncestor(window.PanelScroll).TransformBounds(new Rect(0, 0, button.ActualWidth, button.ActualHeight));
                check(button.IsEnabled && button.Focusable && bounds.Left >= -1 && bounds.Right <= window.PanelScroll.ViewportWidth + 1,
                    $"The initially fitted window can bring {name} into its horizontal viewport");
            }
            window.PanelScroll.ScrollToLeftEnd();
            window.Width = 1440; window.Height = 1000;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
            CheckDesignWidths(window, true, check);
            CheckHeader(window.OriginalHeader, window.OriginalPanelTitle, "1", "panel-original-close.svg", check);
            CheckHeader(window.SourceHeader, window.SourcePanelTitle, "2", "panel-source-close.svg", check);
            CheckHeader(window.Braille.Header, window.Braille.PanelTitle, "3", "panel-braille-close.svg", check);
            var activate = typeof(MainWindow).GetMethod("SetActiveEditor", BindingFlags.NonPublic | BindingFlags.Instance)!;
            activate.Invoke(window, [window.PageEditor.Editor]);
            check(window.SourcePanelTitle.FontWeight == FontWeights.SemiBold && window.OriginalPanelTitle.FontWeight == FontWeights.Normal && window.Braille.PanelTitle.FontWeight == FontWeights.Normal,
                "Source focus marks only the source header active");
            activate.Invoke(window, [window.Braille.Editor]);
            check(window.Braille.PanelTitle.FontWeight == FontWeights.SemiBold && window.SourcePanelTitle.FontWeight == FontWeights.Normal,
                "Braille focus moves the active header without changing panel content");
            check(FigmaBadge(window.OriginalHeader, "1") && FigmaBadge(window.SourceHeader, "2") && FigmaBadge(window.Braille.Header, "3"),
                "All three Figma number badges retain their style when the active panel changes");
            activate.Invoke(window, [null]);
            check(window.OriginalPanelTitle.FontWeight == FontWeights.SemiBold && window.Braille.PanelTitle.FontWeight == FontWeights.Normal,
                "Original selection marks the original header active");
            activate.Invoke(window, [window.PageEditor.Editor]);
            if (Environment.GetEnvironmentVariable("PANEL_THREE_CAPTURE") is { Length: > 0 } threeCapture)
                await WindowsSmoke.RenderElementAsync(window, threeCapture);
            var originalBlock = window.Session.ActiveBlock!; var originalMetadata = originalBlock.Metadata;
            originalBlock.Metadata = new(Type: "image", SelectedIndex: 2, Alternatives: [
                new("생략", "", null, "", null), new("짧은 제목", "네 지역의 기후 특성", null, "⠁", null),
                new("개조식", "그림: 네 지역의 기후 특성\n1. (가) 최한월 −6℃, 연강수량 1,300㎜", null, "⠲⠗⠡⠌⠤⠀⠙⠘⠤", null),
                new("줄글", "네 지역의 기후 특성을 비교한 그림입니다.", null, "⠃", null)]);
            await window.ExecuteAsync("drafts"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
            check(window.DraftDock.IsVisible && window.DraftChoiceGrid.Children.Count == 4 &&
                Math.Abs(window.DraftDock.ActualWidth - window.SourcePanel.ActualWidth - window.Braille.ActualWidth) < 3 &&
                window.DraftTextPreview.Text.StartsWith("그림: 네 지역", StringComparison.Ordinal) && window.DraftBraillePreview.Text.StartsWith("⠲", StringComparison.Ordinal),
                "Alternative draft spans the source and braille panels with all real choices and both previews");
            if (Environment.GetEnvironmentVariable("PANEL_DRAFT_CAPTURE") is { Length: > 0 } draftCapture)
                await WindowsSmoke.RenderElementAsync(window, draftCapture);
            typeof(MainWindow).GetMethod("CloseDraftDock", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
            originalBlock.Metadata = originalMetadata;
            window.SourcePageBar.SetPages(Enumerable.Range(1, 20), Enumerable.Range(1, 20), 10); window.UpdateLayout();
            check(Descendants(window.SourcePageBar).OfType<TextBox>().Single().Text == "10"
                && Descendants(window.SourcePageBar).OfType<TextBlock>().Any(t => t.Text == "/ 20") && window.SourcePageBar.ActualHeight == 24,
                "The 24px header navigator shows the current page and total even on a long document");
            typeof(MainWindow).GetMethod("RefreshEditorStatus", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
            window.Width = 1280; window.Height = 800; window.UpdateLayout();
            check(window.OriginalPanel.ActualWidth >= 260 && window.SourcePanel.ActualWidth >= 300 && window.Braille.ActualWidth >= 300,
                "Three panels remain usable at the 1280-pixel minimum");
            // WindowBounds.Fit lowers the minimum on a smaller monitor; emulate that to exercise panel scrolling.
            window.MinWidth = 480; window.Width = 480; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
            check(window.PanelScroll.ScrollableWidth > 0 && window.OriginalPanel.ActualWidth >= 260 && window.SourcePanel.ActualWidth >= 300 && window.Braille.ActualWidth >= 300,
                "A 480px editor scrolls horizontally while keeping all three panels usable");
            foreach (var (header, name) in new[] { (window.OriginalHeader, "원본 패널 숨기기"), (window.SourceHeader, "묵자 패널 숨기기"), (window.Braille.Header, "점자 패널 숨기기") })
            {
                var button = Descendants(header).OfType<Button>().Single(b => AutomationProperties.GetName(b) == name);
                button.Focus();
                Rect bounds = Rect.Empty;
                for (int frame = 0; frame < 30; frame++)
                {
                    button.BringIntoView();
                    await Task.Delay(16); window.UpdateLayout();
                    bounds = button.TransformToAncestor(window.PanelScroll).TransformBounds(new Rect(0, 0, button.ActualWidth, button.ActualHeight));
                    if (bounds.Left >= -1 && bounds.Right <= window.PanelScroll.ViewportWidth + 1) break;
                }
                check(button.IsEnabled && button.Focusable && bounds.Left >= -1 && bounds.Right <= window.PanelScroll.ViewportWidth + 1,
                    $"The 480px editor can scroll to {name} (bounds={bounds}, viewport={window.PanelScroll.ViewportWidth}, offset={window.PanelScroll.HorizontalOffset})");
            }
            window.PanelScroll.ScrollToLeftEnd();
            var path = Path.Combine(directory, "panel-design.txt");
            await File.WriteAllTextAsync(path, "텍스트 파일 내용");
            await window.OpenAsync(path); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
            check(!window.OriginalToggle.IsChecked && window.SourcePanelNumber.Text == "1" && window.Braille.PanelNumber.Text == "2",
                "TXT uses two panels with Figma numbering");
            check(FigmaBadge(window.SourceHeader, "1") && FigmaBadge(window.Braille.Header, "2"),
                "Two-panel TXT numbering retains the Figma badge color and typography");
            window.Width = 1440; window.Height = 1000; window.UpdateLayout();
            CheckDesignWidths(window, false, check);
            if (Environment.GetEnvironmentVariable("PANEL_TWO_CAPTURE") is { Length: > 0 } twoCapture)
                await WindowsSmoke.RenderElementAsync(window, twoCapture);

            var model = window.Session;
            typeof(MainWindow).GetField("autoTranslate", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, false);
            model.PageStatuses[1] = "COMPLETED"; model.PageStatuses[2] = "FAILED"; model.PageStatuses[3] = "COMPLETED";
            model.Add(new EditorBlock { Id = 91, Page = 3, Source = "셋째 쪽", Braille = "⠁" });
            typeof(MainWindow).GetMethod("RefreshEditorStatus", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
            window.UpdateLayout();
            var pageInput = Descendants(window.SourcePageBar).OfType<TextBox>().Single();
            EnterPage(pageInput, 2);
            check(window.PageEditor.DisplayedPage == 2 && window.PageEditor.Editor.Text.Length == 0 && window.PageEditor.Editor.IsReadOnly,
                "The source header opens a blank read-only view of an identified failed page");
            EnterPage(pageInput, 3);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); await Task.Delay(500);
            check(window.PageEditor.DisplayedPage == 3 && window.PageEditor.Editor.Text == "셋째 쪽" && model.ActiveBlock?.Page == 1,
                "The source page bar browses page three without selecting a different working block");
            var thirdBlock = model.Blocks.First(b => b.Page == 3);
            await window.SelectOriginalRegionAsync(thirdBlock);
            check(model.ActiveBlock == thirdBlock && window.DisplayedOriginalPage == 3 && window.PageEditor.DisplayedPage == 3,
                "Selecting an original region synchronizes the other panels to that block");

            model.Blocks.Clear(); model.ActiveBlock = null; model.PageStatuses.Clear();
            model.Layout = new(Columns: 8, Lines: 4, StartPage: 7, PageNumbers: "출력 안 함", Footer: "출력 안 함");
            model.Add(new EditorBlock { Id = 1, Page = 1, Source = "원문", Braille = string.Join("\n", Enumerable.Repeat("⠁", 16)), HasManualBraille = true });
            await Task.Delay(200); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var grid = window.Braille.GridView; grid.GoToFace(1); await Task.Delay(100); window.UpdateLayout();
            var faceInput = Descendants(window.Braille.FacePageBar).OfType<TextBox>().Single();
            check(grid.Items.Count == 4 && faceInput.IsEnabled && Descendants(window.Braille.FacePageBar).OfType<TextBlock>().Any(t => t.Text == "/ 4"), "The braille page bar uses actual composed face count");
            EnterPage(faceInput, 2);
            await Task.Delay(500);
            check(grid.CaretCoordinates is { Face: 2 }, $"The braille page bar navigates to the real second face (face {grid.CaretCoordinates?.Face}; status {model.Status})");
        }
        finally { await window.ReleaseResourcesAsync(); window.PrepareSmokeShutdown(); window.Close(); }
    }

    private static void EnterPage(TextBox input, int page)
    {
        input.Focus();
        input.Text = page.ToString();
        input.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(input)!, 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
    }

    private static void CheckDesignWidths(MainWindow window, bool hasOriginal, Action<bool, string> check)
    {
        // The operating system may cap an actual window to the small monitor's maximum
        // size. Arrange the real panel grid at the Figma width without assuming that
        // the containing window itself can grow to that size, then restore its width.
        var naturalWidth = window.PanelGrid.Width;
        try
        {
            window.PanelGrid.Width = 1440; window.UpdateLayout();
            var original = window.OriginalPanel.ActualWidth;
            var source = window.SourcePanel.ActualWidth;
            var braille = window.Braille.ActualWidth;
            check(Math.Abs(window.PanelGrid.ActualWidth - 1440) < 2 &&
                  Math.Abs(source - (hasOriginal ? 554 : 854)) < 2 && Math.Abs(braille - 586) < 2 &&
                  (!hasOriginal || Math.Abs(original - 300) < 2),
                $"{(hasOriginal ? "Three panels" : "TXT panels")} use the Figma proportions when their real grid is 1440px wide " +
                $"(window={window.ActualWidth:F1}, grid={window.PanelGrid.ActualWidth:F1}, original={original:F1}, source={source:F1}, braille={braille:F1})");
        }
        finally { window.PanelGrid.Width = naturalWidth; window.UpdateLayout(); }
    }

    private static void CheckHeader(Border header, TextBlock title, string number, string iconFile, Action<bool, string> check)
    {
        var labels = Descendants(header).OfType<TextBlock>().ToArray();
        var badge = labels.FirstOrDefault(t => t.Text == number)?.Parent as Border;
        check(title.FontSize == 14 && FigmaBadge(header, number) && badge is not null &&
              title.TransformToAncestor(header).Transform(new Point()).X > badge.TransformToAncestor(header).Transform(new Point()).X,
            $"{title.Text} header displays the circular number before its title");
        var icon = Descendants(header).OfType<SvgViewbox>().FirstOrDefault(i => i.Source?.ToString().EndsWith("/" + iconFile, StringComparison.Ordinal) == true);
        check(icon is { Width: 16, Height: 16, Drawings.Bounds.IsEmpty: false }, $"{title.Text} header renders its original Figma close icon");
        var actions = Descendants(header).OfType<Button>().Where(b => b.IsVisible).ToArray();
        check(actions.Length >= 2 && actions.All(b => !string.IsNullOrWhiteSpace(AutomationProperties.GetName(b))),
            $"{title.Text} header controls have accessible names");
    }

    private static bool FigmaBadge(Border header, string number)
    {
        var text = Descendants(header).OfType<TextBlock>().FirstOrDefault(t => t.Text == number);
        return text is { FontSize: 12, Foreground: SolidColorBrush { Color: var ink }, Parent: Border { Width: 18, Height: 18, CornerRadius.TopLeft: 9, Background: SolidColorBrush { Color: var color } } }
            && text.FontFamily.Source == Semojum.Desktop.Controls.BundledFonts.Interface.Source
            && (header.BorderThickness == new Thickness(0, 2, 0, 0) ? header.Background is SolidColorBrush { Color: var activeFace } && activeFace == Colors.White && color == Color.FromRgb(0, 95, 184) && ink == Colors.White
                : header.BorderThickness == new Thickness(0, 0, 0, 1) && color == Color.FromRgb(240, 240, 240) && ink == Color.FromRgb(95, 95, 95));
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        yield return parent;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(parent, i))) yield return child;
    }
}
