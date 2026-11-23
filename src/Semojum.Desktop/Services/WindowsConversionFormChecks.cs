using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Semojum.Core;
using Semojum.Core.Integration;
using Semojum.Desktop.Dialogs;
using Semojum.Feature.Settings;
using static Semojum.Desktop.Services.WindowsSettingsHelpDesignChecks;

namespace Semojum.Desktop.Services;

internal static class WindowsConversionFormChecks
{
    internal static async Task RunAsync(string directory, Action<bool, string> check)
    {
        var capture = Directory.CreateDirectory(Path.Combine(directory, "conversion-options")).FullName;
        var general = new BrailleForm("builtin:GENERAL_BOOK", "일반 도서", true, null, new(), new());
        var pricing = new ProofPricing(100, 500, [new("MINERU", 0), new("HAIKU", 100), new("SONNET", 300), new("OPUS", 500)]);
        var temp = Directory.CreateTempSubdirectory("nuridot-conversion-");
        var pdf = Path.Combine(temp.FullName, "2026_수능특강_사회문화.pdf");
        var open = new OpenDocumentDialog([general], new(new(), general.FormId, "odd"), pricing: pricing);
        try
        {
            await File.WriteAllBytesAsync(pdf, WindowsSmoke.PdfFixture(128));
            open.SetPaths([pdf]); open.Show();
            await Wait(() => open.FileNames.Text.EndsWith(" · 128쪽", StringComparison.Ordinal));
            await WindowsSmoke.RenderAsync(open, Path.Combine(capture, "open.png"));
            CheckBounds(open, open.FileNames, 203, 69, 356, 32, "filename");
            CheckBounds(open, open.Forms, 203, 113, 356, 32, "form");
            CheckBounds(open, Radio(open, "텍스트 쪽"), 41, 341, 255, 40, "text rate");
            CheckBounds(open, Radio(open, "기본 MinerU"), 41, 425, 518, 40, "first model");
            CheckBounds(open, Button(open, "변환 시작"), 487, 665, 88, 32, "start");
            check(Has(open, "12800 크레딧"), "Opening a real 128-page PDF multiplies its actual page count by the selected rate");
            Radio(open, "그림 쪽").IsChecked = true; Radio(open, "고급 Sonnet").IsChecked = true;
            check(Has(open, "102400 크레딧") && open.Settings.Layout.AdvancedAi && Equals(JobClient.LayoutBody(open.Settings.Layout)["proofModel"], "SONNET"),
                "Changing page estimate and proof model updates price and submits the selected model");
            open.SetPaths(["first.png", "second.png"]);
            check(Has(open, "1600 크레딧") && open.FileNames.Text.EndsWith(" · 2쪽"), "Ordered images use their selected count for the estimate");
            open.SetPaths(["draft.hwpx"]);
            check(Has(open, "쪽당 800 크레딧 · 쪽수 확인 후 계산"), "An unpaginated input shows its unit rate without inventing a page count");
            open.SetPaths([pdf]);
            open.SetPaths(["draft.txt"]);
            await Task.Delay(100);
            check(Has(open, "0 크레딧") && !Radio(open, "고급 Sonnet").IsEnabled && !JobClient.LayoutBody(open.Settings.Layout).ContainsKey("proofModel"),
                "Local TXT clears the model from requests and displays a zero cost");
            check(open.FileNames.Text == "draft.txt", "A stale PDF metadata response never replaces a newly chosen filename");
        }
        finally { open.Close(); temp.Delete(true); }

        var reconvert = new PageReconversionDialog(13, Enumerable.Range(1, 16).ToHashSet(), pricing,
            (_, _, _) => Task.FromResult("test"), _ => false, "SONNET");
        reconvert.Show();
        try
        {
            await WindowsSmoke.RenderAsync(reconvert, Path.Combine(capture, "reconvert.png"));
            var range = Children(reconvert).OfType<TextBox>().Single();
            CheckBounds(reconvert, range, 275, 97, 200, 32, "range");
            CheckBounds(reconvert, Radio(reconvert, "고급 Sonnet"), 41, 401, 518, 40, "current document model");
            CheckBounds(reconvert, Button(reconvert, "다시 변환"), 487, 553, 88, 32, "reconvert");
            check(Has(reconvert, "400 크레딧") && Radio(reconvert, "고급 Sonnet").IsChecked == true,
                "Reconversion preselects the current document model and prices the current page");
            Children(reconvert).OfType<RadioButton>().Single(r => Equals(r.Content, "쪽 지정")).IsChecked = true;
            range.Text = "3, 5-7";
            check(Has(reconvert, "1600 크레딧"), "Reconversion estimates the four distinct valid pages in a range");
        }
        finally { reconvert.Close(); }

        var fixture = new Fixture();
        using var http = new HttpClient(fixture);
        var api = new SemojumApiClient(http); await api.LoginAsync("fixture", "fixture");
        var defaults = new DefaultsDialog(new(api));
        Exception? error = null;
        defaults.ContentRendered += (_, _) => defaults.Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                await Wait(() => Button(defaults, "기본값 복원").IsEnabled);
                Button(defaults, "기본값 복원").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                await WindowsSmoke.RenderAsync(defaults, Path.Combine(capture, "defaults.png"));
                CheckBounds(defaults, Children(defaults).OfType<ComboBox>().Single(c => c.SelectedItem is BrailleForm), 203, 97, 316, 32, "default form");
                CheckBounds(defaults, Button(defaults, "기본값 복원"), 25, 621, 88, 32, "restore");
                CheckBounds(defaults, Button(defaults, "확인"), 447, 621, 88, 32, "save");
                Radio(defaults, "영문 정자(1급)").IsChecked = true; Radio(defaults, "페이지 번호 표시줄 면 모든").IsChecked = true;
                Button(defaults, "확인").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            }
            catch (Exception ex) { error = ex; defaults.Close(); }
        }), DispatcherPriority.ApplicationIdle);
        var accepted = defaults.ShowDialog();
        if (error is not null) throw error;
        check(accepted == true && fixture.PutCount == 1 && fixture.Saved is { } data
            && data.GetProperty("englishGrade").GetString() == "grade1" && data.GetProperty("pageNumberLine").GetString() == "every",
            "Default form saves the selected English grade and page number line through the authenticated settings API");

        void CheckBounds(Window w, FrameworkElement e, int x, int y, int width, int height, string label)
        {
            var point = e.TranslatePoint(new Point(), w);
            check(Math.Abs(point.X-x) <= 1 && Math.Abs(point.Y-y) <= 1 && Math.Abs(e.ActualWidth-width) <= 1 && Math.Abs(e.ActualHeight-height) <= 1,
                $"{w.Title} {label}: ({point.X},{point.Y}) {e.ActualWidth}x{e.ActualHeight} matches Figma ({x},{y}) {width}x{height}");
        }
    }
    private static Button Button(Window w, string label) => Children(w).OfType<Button>().Single(b => Equals(b.Content, label));
    private static bool Has(Window w, string text) => Children(w).OfType<TextBlock>().Any(t => t.Text == text);
    private static IEnumerable<DependencyObject> Children(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in Children(child)) yield return nested;
        }
    }
    private static async Task Wait(Func<bool> ready)
    {
        var deadline = DateTime.UtcNow.AddSeconds(6);
        while (!ready() && DateTime.UtcNow < deadline) await Task.Delay(20);
        if (!ready()) throw new TimeoutException("Conversion options did not finish loading");
    }
}
