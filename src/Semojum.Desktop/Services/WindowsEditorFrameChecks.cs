using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SharpVectors.Converters;

namespace Semojum.Desktop.Services;

internal static class WindowsEditorFrameChecks
{
    internal static async Task RunAsync(Action<bool, string> check)
    {
        var window = new MainWindow(smoke: true, testWithoutAccount: true);
        var results = new List<(bool Passed, string Description)>();
        var captures = Environment.GetEnvironmentVariable("EDITOR_FRAME_CAPTURE_DIR");
        if (captures is not null) Directory.CreateDirectory(captures);
        window.Show();
        try
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            window.WindowState = WindowState.Normal;
            await CheckFrames("C1");
            await window.ExecuteAsync("new");
            await CheckFrames("C3");
            window.HidePanel("hideSource"); window.HidePanel("hideBraille");
            await CheckFrames("C2");
            window.MinHeight = 300; window.Width = 480; window.Height = 320;
            await Layout();
            var recoveryScroll = Children<ScrollViewer>(window.EmptyPanelActions).Single();
            recoveryScroll.ScrollToEnd();
            await Layout();
            var restore = Tile("all");
            var buttonTop = restore.TranslatePoint(new Point(), recoveryScroll).Y;
            results.Add((recoveryScroll.ScrollableHeight > 0 && buttonTop >= 0
                && buttonTop + restore.ActualHeight <= recoveryScroll.ActualHeight + 1,
                "A short editor can scroll to the complete restore-all button"));
            foreach (var (target, label) in new[] { ("original","원본 패널"), ("source","묵자 패널"), ("braille","점자 패널") })
            {
                Restore(target);
                await Layout();
                results.Add((window.StatusPage.Text == label && !window.EmptyPanelActions.IsVisible, $"Restoring {target} shows its own status"));
                window.HidePanel(target switch { "original" => "hideOriginal", "source" => "hideSource", _ => "hideBraille" });
            }
            Restore("all");
            await Layout();
            results.Add((window.OriginalPanel.IsVisible && window.SourcePanel.IsVisible && window.BrailleContainer.IsVisible
                && window.CaretStatus.IsVisible && window.StatusPage.Text == "묵자 패널", "Restore all returns to the source panel and live caret"));
            foreach (var result in results.Where(r => !r.Passed)) Console.Error.WriteLine(result.Description);
            foreach (var result in results) check(result.Passed, result.Description);
        }
        finally { await window.ReleaseResourcesAsync(); window.PrepareSmokeShutdown(); window.Close(); }

        Button Tile(string tag) => Children<Button>(window.EmptyPanelActions).Single(b => Equals(b.Tag,tag));
        void Restore(string tag) => Tile(tag).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        async Task Layout()
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();
        }
        async Task CheckFrames(string state)
        {
            foreach (var (width,height) in new[] { (1440,1000), (1280,800) })
            {
                window.Width = width; window.Height = height; await Layout();
                void At(FrameworkElement element, double x, double y, double w, double h, string name)
                {
                    var p = element.TranslatePoint(new Point(), window);
                    var ok = element.IsVisible && Math.Abs(p.X-x)<=1.5 && Math.Abs(p.Y-y)<=1.5
                        && Math.Abs(element.ActualWidth-w)<=1.5 && Math.Abs(element.ActualHeight-h)<=1.5;
                    results.Add((ok,$"{state} {width} {name}: expected {x:F1},{y:F1} {w:F1}x{h:F1}, actual {p.X:F1},{p.Y:F1} {element.ActualWidth:F1}x{element.ActualHeight:F1}"));
                }
                At(window.EditorRibbon,0,32,width,120,"ribbon");
                At(window.DocumentTabs,0,152,width,34,"document tabs");
                At(window.StatusBar,0,height-28,width,28,"status bar");
                if (state == "C1")
                {
                    double tabX = 8;
                    foreach (TabItem tab in window.EditorRibbon.Items)
                    {
                        double tabWidth = tab.Header switch { "홈" => 40, "도움말" => 61, _ => 49 };
                        At(tab,tabX,32,tabWidth,32,"tab "+tab.Header);
                        tabX += tabWidth;
                    }
                    foreach (var (action,x,w) in new[] { ("undo",12,50), ("redo",66,53), ("find",214,63), ("goto",281,48), ("memo",346,53) })
                    {
                        var command = Children<Button>(window.EditorRibbon).Single(b => Equals(b.Tag,action));
                        At(command,x,68,w,68,action);
                    }
                }
                if (state == "C2")
                {
                    At(window.EmptyPanelActions,0,186,width,height-214,"recovery area");
                    var left = (width-512)/2; var top = 186 + (height-214-180)/2;
                    int i = 0;
                    foreach (var tag in new[] { "original","source","braille" })
                    {
                        var tile = Tile(tag); var x = left + 176*i++;
                        At(tile,x,top,160,128,tag+" tile");
                        var icon = Children<SvgViewbox>(tile).Single();
                        var iconSlot = Children<Grid>(tile).Single(g => g.Width == 24 && g.Height == 24);
                        At(iconSlot,x+68,top+38,24,24,tag+" icon slot");
                        results.Add((icon.Drawings is { Bounds.IsEmpty: false } && icon.Stretch == Stretch.None,
                            $"{state} {width} {tag} uses the original-size Figma icon"));
                        var label = Children<TextBlock>(tile).Single();
                        At(label,x+(160-label.ActualWidth)/2,top+82,label.ActualWidth,20,tag+" label");
                    }
                    At(Tile("all"),(width-88)/2,top+148,88,32,"restore all");
                    results.Add((window.StatusPage.Text == "패널 없음" && !window.CaretStatus.IsVisible && !window.BlockStatus.IsVisible
                        && window.StatusOptions.IsVisible && window.StatusOptions.Text.Contains("급"), $"C2 {width} shows no panel, no stale position and the document grades"));
                }
                else
                {
                    var accountArrow = Children<SvgViewbox>(window.AccountMenuButton).Single();
                    results.Add((accountArrow.Width == 12 && accountArrow.Source.ToString().EndsWith("/ribbon-chevd.svg", StringComparison.Ordinal)
                        && Children<TextBlock>(window.EditorRibbon).Any(t => t.Text == "편집" && t.FontSize == 12),
                        $"{state} {width} Home uses the Figma account arrow and group label size"));
                    bool original = state == "C1";
                    double originalWidth = original ? width*300d/1440 : 0;
                    double brailleX = width*854d/1440;
                    var bodyHeight = height-214-(original ? 56 : 0);
                    if (original) At(window.OriginalPanel,0,186,originalWidth,bodyHeight,"original");
                    At(window.SourcePanel,originalWidth,186,brailleX-originalWidth,bodyHeight,"source");
                    At(window.BrailleContainer,brailleX,186,width-brailleX,bodyHeight,"braille");
                    var view = window.PageEditor.Editor.TextArea.TextView;
                    var line = view.GetVisualLine(window.PageEditor.Editor.TextArea.Caret.Line);
                    if (line is null) throw new InvalidOperationException("The source caret line did not render.");
                    var top = view.TranslatePoint(new Point(view.ActualWidth-28, line.VisualTop-view.VerticalOffset),window);
                    var bitmap = new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    var pixels = new byte[((int)Math.Ceiling(line.Height)+1)*4];
                    bitmap.CopyPixels(new Int32Rect((int)top.X,(int)top.Y,1,pixels.Length/4),pixels,4,0);
                    // Pbgra32 stores blue, green, red, alpha. The old outline had more green than blue.
                    bool tintMatches = pixels[4] == 251 && pixels[5] == 246 && pixels[6] == 242;
                    results.Add((tintMatches && Enumerable.Range(0,pixels.Length/4).All(i => pixels[4*i+1] <= pixels[4*i]),
                        $"{state} {width} renders the Figma current-line fill without a green outline"));
                    if (original) At(window.LineStrip,0,height-84,width,56,"line translation");
                    else results.Add((!window.LineStrip.IsVisible && !window.OriginalPanel.IsVisible,$"C3 {width} gives the TXT panels the entire editor body"));
                }
                if (captures is not null) await WindowsSmoke.RenderElementAsync(window,Path.Combine(captures,$"{state}-{width}x{height}.png"));
            }
        }
    }

    private static IEnumerable<T> Children<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i=0; i<VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent,i);
            if (child is T element) yield return element;
            foreach (var elementBelow in Children<T>(child)) yield return elementBelow;
        }
    }
}
