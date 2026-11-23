using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Semojum.Core;
using Semojum.Desktop.Dialogs;

namespace Semojum.Desktop.Services;

internal static class WindowsExportFileChecks
{
    internal static async Task RunAsync(string directory, Action<bool, string> check)
    {
        var folder = Path.Combine(directory, "range-output-" + Guid.NewGuid()); Directory.CreateDirectory(folder);
        var window = new MainWindow(testWithoutAccount: true);
        var notices = new List<(string Message, string Detail, string[] Labels, bool Red)>(); var rejectExisting = false; var clicked = -2;
        window.ExportConfirmation = notice =>
        {
            notices.Add((notice.MessageText, notice.DetailText, notice.ActionButtons.Select(b => (string)b.Content).ToArray(),
                notice.ActionButtons[0].Background is SolidColorBrush { Color: var c } && c == Color.FromRgb(196, 43, 28)));
            if (notice.MessageText == "내보내기 전에 확인할 자리가 있습니다.")
            {
                notice.Loaded += (_, _) => notice.Dispatcher.BeginInvoke(new Action(() => notice.ActionButtons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent))));
                notice.ShowDialog();
                return notice.Choice;
            }
            if (!rejectExisting || notice.MessageText != "형식 변경으로 선택한 파일이 이미 있습니다.") { notice.Close(); return 0; }
            // Show the real message window once and press 「취소」 so the button, not only the hook, decides.
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (_, _) => { timer.Stop(); notice.Close(); };
            notice.Loaded += (_, _) => notice.Dispatcher.BeginInvoke(new Action(() => notice.ActionButtons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent))));
            timer.Start(); notice.ShowDialog(); timer.Stop(); clicked = notice.Choice; return notice.Choice;
        };
        try
        {
            window.Show();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var document = new DocumentSession { Layout = new(Columns: 16, Lines: 4, PageNumbers: "출력 안 함", Footer: "모든 페이지"), FooterText = "바" };
            document.Add(new EditorBlock { Id = 1, Page = 1, Source = "가나다", Braille = "⠫⠉⠊", HasManualBraille = true });
            document.Add(new EditorBlock { Id = 2, Page = 2, Source = "다른 쪽", Braille = "⠙", HasManualBraille = true });
            var path = Path.Combine(folder, "fixture.smj"); await SmjFile.SaveAsync(path, SmjFile.Snapshot(document));
            Console.WriteLine("RANGE-TEST fixture saved");
            var reopened = await SmjFile.LoadAsync(path);
            typeof(MainWindow).GetMethod("SetSession", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [reopened, path, false, false]);
            Console.WriteLine("RANGE-TEST session loaded");
            var method = typeof(MainWindow).GetMethod("ExportAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

            async Task Export(string proposed, int format, string chosen, bool rejectOverwrite = false)
            {
                rejectExisting = rejectOverwrite;
                window.ExportRangeSelection = async range =>
                {
                    range.Owner = window;
                    var interaction = window.Dispatcher.InvokeAsync(async () =>
                    {
                        try
                        {
                            WindowsExportRangeChecks.Choice(range, chosen is ".txt — 점자" or ".brf" ? "점자 페이지" : "원본 페이지").IsChecked = true;
                            var save = WindowsExportRangeChecks.Save(range);
                            var until = DateTime.UtcNow.AddSeconds(15);
                            while (!save.IsEnabled && DateTime.UtcNow < until) await Task.Delay(20);
                            if (!save.IsEnabled) throw new TimeoutException("Export page count did not complete");
                            WindowsExportRangeChecks.Choice(range, chosen.Replace(" — ", " ")).IsChecked = true;
                            WindowsExportRangeChecks.Field(range,false).Text = "1";
                            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        }
                        finally { if (range.IsVisible) range.Close(); }
                    }, DispatcherPriority.ApplicationIdle);
                    var accepted = range.ShowDialog() == true;
                    await interaction.Task.Unwrap();
                    return accepted;
                };
                Console.WriteLine("RANGE-TEST exporting " + chosen);
                await ((Task)method.Invoke(window, [proposed, format, true])!).WaitAsync(TimeSpan.FromSeconds(20));
                Console.WriteLine("RANGE-TEST export returned " + chosen);
            }

            var proposedBrf = Path.Combine(folder, "source.brf");
            await Export(proposedBrf, 1, ".txt — 원문");
            var sourcePath = Path.ChangeExtension(proposedBrf, ".txt");
            check(File.Exists(sourcePath) && !File.Exists(proposedBrf) && await File.ReadAllTextAsync(sourcePath) == "가나다",
                "Changing BRF to source TXT writes matching extension and source text");

            var proposedMd = Path.Combine(folder, "source-md.brf");
            await Export(proposedMd, 1, ".md");
            check(await File.ReadAllTextAsync(Path.ChangeExtension(proposedMd, ".md")) == "가나다" && !File.Exists(proposedMd),
                "Source Markdown saves only the selected original page with .md extension");
            var proposedBraille = Path.Combine(folder, "braille-unicode.brf");
            await Export(proposedBraille, 2, ".txt — 점자");
            var unicode = await File.ReadAllTextAsync(Path.ChangeExtension(proposedBraille, ".txt"));
            check(unicode.Contains("⠫⠉⠊") && !unicode.Contains("가나다") && !File.Exists(proposedBraille),
                "Braille TXT saves Unicode braille with matching .txt extension");
            foreach (var (index, extension) in new[] { (1, ".brf"), (2, ".txt"), (3, ".txt") })
            {
                var fullPath = Path.Combine(folder, "full-" + index + extension);
                await (Task)method.Invoke(window, [fullPath, index, false])!;
                var saved = await File.ReadAllTextAsync(fullPath);
                string decoded = "";
                if (index == 1)
                {
                    await using var reader = EngineRuntime.Create();
                    decoded = DocumentExport.BrailleText(await new Semojum.Core.Integration.LocalBrailleEngine(reader).ReadBrfAsync(saved, CancellationToken.None));
                }
                check(index == 1 ? decoded.Contains("⠫⠉⠊") && decoded.Contains("⠙")
                    : index == 2 ? saved.Contains("가나다") && saved.Contains("다른 쪽") : saved.Contains("⠫⠉⠊") && saved.Contains("⠙"),
                    "Save As without a range retains whole-document format " + index);
            }
            var proposedTxt = Path.Combine(folder, "braille.txt");
            await Export(proposedTxt, 2, ".brf");
            var braillePath = Path.ChangeExtension(proposedTxt, ".brf");
            var bytes = await File.ReadAllBytesAsync(braillePath);
            check(!File.Exists(proposedTxt) && bytes.Length > 0 && bytes[^1] == 12 && bytes.All(b => b < 128)
                && DocumentExport.BrfPages(System.Text.Encoding.ASCII.GetString(bytes)).Length == 1
                && System.Text.Encoding.ASCII.GetString(bytes).Split("\r\n").Count(line => line.Trim().Length > 0) >= 2
                && !notices.Any(n => n.Message == "내보내기 전에 확인할 자리가 있습니다." && n.Detail.Contains("조판 빈 줄")),
                "Changing source TXT to BRF writes the selected page with its footer and matching extension; edited braille without blank lines is not questioned");
            await Export(proposedTxt, 2, ".brf", rejectOverwrite: true);
            check((await File.ReadAllBytesAsync(braillePath)).SequenceEqual(bytes) && !File.Exists(proposedTxt) && clicked == 1
                && notices.Any(n => n.Message == "형식 변경으로 선택한 파일이 이미 있습니다." && n.Labels.SequenceEqual(["덮어쓰기", "취소"]) && n.Red),
                "Pressing 「취소」 in the red overwrite notice after an extension change preserves the existing BRF");

            // A-5: the engine lays out page blank lines (translate_page). Only an edited block whose typed blank line meets one is questioned.
            var first = reopened.Blocks[0]; first.Metadata = new(Type: "title", HeadingLevel: 2); first.Braille = "⠫⠉⠊\n\n";
            notices.Clear(); await Export(proposedTxt, 2, ".brf");
            check(notices.Count(n => n.Message == "내보내기 전에 확인할 자리가 있습니다." && n.Detail.Contains("조판 빈 줄")) == 1
                && notices.Single(n => n.Message == "내보내기 전에 확인할 자리가 있습니다." && n.Detail.Contains("조판 빈 줄")).Labels.SequenceEqual(["계속", "취소"]) && !notices.Single(n => n.Message == "내보내기 전에 확인할 자리가 있습니다." && n.Detail.Contains("조판 빈 줄")).Red
                && first.Braille == "⠫⠉⠊\n\n" && first.HasManualBraille,
                "A manually edited title whose typed blank line meets the layout blank line asks once before export and keeps its cells");
            first.Braille = "⠫⠉⠊"; first.Translation = BrailleTranslation.Plain("⠫⠉⠊") with { SourceText = first.Source }; first.HasManualBraille = false;
            notices.Clear(); await Export(proposedTxt, 2, ".brf");
            check(!notices.Any(n => n.Message == "내보내기 전에 확인할 자리가 있습니다." && n.Detail.Contains("조판 빈 줄")) && File.Exists(braillePath),
                "An engine-translated block never raises the blank-line notice on export");
        }
        finally { window.PrepareSmokeShutdown(); window.Close(); await window.ReleaseResourcesAsync(); Directory.Delete(folder, true); }
    }
}
