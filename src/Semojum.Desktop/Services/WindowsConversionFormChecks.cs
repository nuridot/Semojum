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
        var temp = Directory.CreateTempSubdirectory("nuridot-conversion-");
        var pdf = Path.Combine(temp.FullName, "2026_수능특강_사회문화.pdf");
        var open = new OpenDocumentDialog([general], new(new(), general.FormId, "odd"));
        try
        {
            await File.WriteAllBytesAsync(pdf, WindowsSmoke.PdfFixture(128));
            open.SetPaths([pdf]); open.Show();
            await Wait(() => open.FileNames.Text.EndsWith(" · 128쪽", StringComparison.Ordinal));
            await WindowsSmoke.RenderAsync(open, Path.Combine(capture, "open.png"));
            CheckBounds(open, open.FileNames, 203, 69, 356, 32, "filename");
            CheckBounds(open, open.Forms, 203, 113, 356, 32, "form");
            CheckBounds(open, Radio(open, "기본"), 41, 309, 518, 40, "first level");
            CheckBounds(open, Radio(open, "최고급"), 41, 441, 518, 40, "last level");
            CheckBounds(open, Button(open, "변환 시작"), 487, 517, 88, 32, "start");
            check(Radio(open, "기본").IsChecked == true && LevelsOnly(open), "Upload defaults to the basic level and shows only Korean tier labels");
            foreach (var (label, code) in new[] { ("기본", "MINERU"), ("중급", "HAIKU"), ("고급", "SONNET"), ("최고급", "OPUS") })
            {
                Radio(open, label).IsChecked = true;
                check(open.Settings.Layout.ProofModel == code && open.Settings.Layout.AdvancedAi == (code != "MINERU"),
                    $"Upload level {label} selects {code}");
            }
            open.SetPaths(["first.png", "second.png"]);
            check(open.FileNames.Text.EndsWith(" · 2쪽") && open.Settings.Layout.ProofModel == "OPUS", "Image count is independent of the proofing tier");
            open.SetPaths(["draft.hwpx"]);
            check(open.Settings.Layout.ProofModel == "OPUS", "HWPX retains the selected level");
            open.SetPaths([pdf]);
            open.SetPaths(["draft.txt"]);
            await Task.Delay(100);
            check(!Radio(open, "최고급").IsEnabled && open.Settings.Layout.ProofModel is null && !open.Settings.Layout.AdvancedAi,
                "Local TXT has no proofing model");
            check(open.FileNames.Text == "draft.txt", "A stale PDF metadata response never replaces a newly chosen filename");
        }
        finally { open.Close(); temp.Delete(true); }

        var reconvert = new PageReconversionDialog(13, Enumerable.Range(1, 16).ToHashSet(), _ => false, "MINERU");
        reconvert.Show();
        try
        {
            check(Radio(reconvert, "기본").IsChecked == true, "Reconversion opens with the document's current tier");
            Radio(reconvert, "고급").IsChecked = true;
            await WindowsSmoke.RenderAsync(reconvert, Path.Combine(capture, "reconvert.png"));
            var range = Children(reconvert).OfType<TextBox>().Single();
            CheckBounds(reconvert, range, 275, 97, 200, 32, "range");
            CheckBounds(reconvert, Radio(reconvert, "고급"), 41, 317, 518, 40, "selected level");
            CheckBounds(reconvert, Button(reconvert, "재변환"), 487, 437, 88, 32, "reconvert");
            check(Has(reconvert, "지금 설정") && Children(reconvert).OfType<TextBlock>().Count(t => t.Text == "기본") == 2 && LevelsOnly(reconvert),
                "The current document tier remains basic when another tier is selected for the pages");
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
    private static bool LevelsOnly(Window window)
    {
        string[] oldLabels = ["MinerU", "Haiku", "Sonnet", "Opus", "쪽 유형", "크레딧", "쪽당 추가"];
        return Children(window).OfType<TextBlock>().All(t => !oldLabels.Any(label => t.Text.Contains(label)));
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
