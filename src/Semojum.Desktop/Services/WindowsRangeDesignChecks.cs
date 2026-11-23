using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Semojum.Desktop.Dialogs;
using SharpVectors.Converters;
using static Semojum.Desktop.Services.WindowsExportRangeChecks;

namespace Semojum.Desktop.Services;

internal static class WindowsRangeDesignChecks
{
    internal static async Task RunAsync(Action<bool, string> check, string? directory = null)
    {
        var countCalls = 0;
        var dialog = new ExportRangeDialog([12, 18, 24], () => { countCalls++; return Task.FromResult(640); }, allowFormatChange: true);
        dialog.Show();
        try
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            check(dialog.ActualWidth == 560 && dialog.ActualHeight == 250 && Field(dialog, true).Text == "12" && Field(dialog, false).Text == "24", "Source range opens in the 560×250 Figma window with real document bounds");
            var card = Tree(dialog).OfType<Border>().Single(b => b.CornerRadius.TopLeft == 8 && b.Child is StackPanel);
            check(card.TranslatePoint(new Point(), dialog) == new Point(25, 57) && card.ActualWidth == 510 && card.ActualHeight == 120
                && Field(dialog, true).TranslatePoint(new Point(), dialog) == new Point(203, 101)
                && Field(dialog, false).TranslatePoint(new Point(), dialog) == new Point(332, 101),
                "The range card and both 96px number fields align with the design");
            foreach (var (asset, expected) in new[] { ("range-close.svg", 1), ("layout-step-up.svg", 2), ("layout-step-down.svg", 2) })
            {
                var icons = Tree(dialog).OfType<SvgViewbox>().Where(i => i.Source.ToString().EndsWith(asset)).ToArray();
                check(icons.Length == expected && icons.All(i => i.ActualWidth > 0 && i.ActualHeight > 0 && i.Drawings is { Bounds.IsEmpty: false }), asset + " renders in all original slots");
            }
            await Capture("source", "RANGE_SOURCE_CAPTURE");
            Choice(dialog, "점자 페이지").IsChecked = true;
            check(countCalls == 1 && Field(dialog, true).Text == "1" && Field(dialog, false).Text == "640" && Tree(dialog).OfType<TextBlock>().Any(t => t.IsVisible && t.Text == "총 640면"),
                "Braille scope calculates and displays the real face count");
            Field(dialog, true).Text = "31"; Field(dialog, false).Text = "62";
            await Capture("braille", "RANGE_BRAILLE_CAPTURE");
            Choice(dialog, ".txt 점자").IsChecked = true;
            check(dialog.SelectedFormat == ".txt — 점자" && Choice(dialog, "점자 페이지").IsChecked == true, "Braille TXT retains the braille scope");
            Choice(dialog, "원본 페이지").IsChecked = true; Choice(dialog, ".md").IsChecked = true;
            check(dialog.SelectedFormat == ".md" && Field(dialog, true).Text == "12" && Field(dialog, false).Text == "24", "Source bounds and Markdown are available when returning to source scope");
            Choice(dialog, "점자 페이지").IsChecked = true;
            check(countCalls == 1, "Switching back reuses the successful braille calculation");
        }
        finally { dialog.Close(); }

        var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayed = new ExportRangeDialog([2, 5], () => gate.Task, allowFormatChange: true);
        delayed.Show();
        try
        {
            Choice(delayed, "점자 페이지").IsChecked = true;
            check(!Save(delayed).IsEnabled, "Braille save waits for the physical page count");
            Choice(delayed, "원본 페이지").IsChecked = true;
            Field(delayed, true).Text = "4"; gate.SetResult(17);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            check(Save(delayed).IsEnabled && Field(delayed, true).Text == "4" && Field(delayed, false).Text == "5",
                "An outstanding braille count never overwrites an edited source range");
        }
        finally { delayed.Close(); }

        var attempts = 0;
        var retry = new ExportRangeDialog([], () => ++attempts == 1 ? Task.FromException<int>(new InvalidOperationException("면 계산 실패")) : Task.FromResult(7), brailleOnly: true, format: ".txt — 점자");
        retry.Show();
        try
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            check(!Choice(retry, "원본 페이지").IsEnabled && !Choice(retry, ".brf").IsEnabled && Choice(retry, ".txt 점자").IsChecked == true, "A preview-only export keeps its format and scope locked");
            check(Tree(retry).OfType<TextBlock>().Any(t => t.IsVisible && t.Text == "면 계산 실패"), "The range dialog shows a calculation failure");
            Save(retry).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(attempts == 2 && retry.IsVisible && Field(retry, false).Text == "7" && Field(retry, true).IsEnabled, "A failed preview-only count can be retried before choosing a range");
            if (Environment.GetEnvironmentVariable("RANGE_PREVIEW_CAPTURE") is { Length: > 0 } preview) await WindowsSmoke.RenderAsync(retry, preview);
        }
        finally { retry.Close(); }

        async Task Capture(string name, string variable)
        {
            if (directory is not null) await WindowsSmoke.RenderAsync(dialog, Path.Combine(Directory.CreateDirectory(Path.Combine(directory, "export-range")).FullName, name + ".png"));
            if (Environment.GetEnvironmentVariable(variable) is { Length: > 0 } path) await WindowsSmoke.RenderAsync(dialog, path);
        }
    }
}
