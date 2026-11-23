using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Semojum.Desktop.Dialogs;
using static Semojum.Desktop.Services.WindowsExportRangeChecks;

namespace Semojum.Desktop.Services;

internal static class WindowsAboutDesignChecks
{
    internal static async Task RunAsync(string directory, Action<bool, string> check)
    {
        var editor = new MainWindow(testWithoutAccount: true, startEmpty: true);
        editor.Show();
        try
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var inspect = editor.Dispatcher.InvokeAsync(async () =>
            {
                var dialog = editor.OwnedWindows.OfType<AboutDialog>().Single();
                try
                {
                    var until = DateTime.UtcNow.AddSeconds(16);
                    while (dialog.EngineVersion.Text == "확인 중…" && DateTime.UtcNow < until) await Task.Delay(50);
                    var surface = Tree(dialog).OfType<Border>().Single(b => b.Child is StackPanel && b.CornerRadius.TopLeft == 8);
                    var bounds = new Rect(surface.TranslatePoint(new Point(), dialog), surface.RenderSize);
                    check(dialog.ActualWidth == 440 && dialog.ActualHeight == 302 && bounds == new Rect(25, 57, 390, 172),
                        $"About frame and four-row surface match Figma: {bounds}");
                    check(Version.TryParse(dialog.EngineVersion.Text, out _) && Tree(dialog).OfType<TextBlock>().Any(t => t.Text == "V" + MainWindow.CurrentVersion.ToString(3)),
                        "About reports the running product and a live numeric version from its bundled engine");
                    await WindowsSmoke.RenderAsync(dialog, Path.Combine(directory, "about.png"));
                    Tree(dialog).OfType<Button>().Single(b => Equals(b.Content, "확인")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    check(!dialog.IsVisible, "The About confirmation closes the window");
                }
                finally { if (dialog.IsVisible) dialog.Close(); }
            }, DispatcherPriority.ApplicationIdle);
            await editor.ExecuteAsync("about");
            await inspect.Task.Unwrap();

            var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var waiting = new AboutDialog("1.0.6", () => pending.Task) { Owner = editor };
            waiting.Show(); waiting.Close();
            pending.SetResult("1.2.3"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            check(!waiting.IsVisible && waiting.EngineVersion.Text == "확인 중…", "Closing About during an engine lookup ignores the late result");
        }
        finally { await editor.ReleaseResourcesAsync(); editor.PrepareSmokeShutdown(); editor.Close(); }
    }
}
