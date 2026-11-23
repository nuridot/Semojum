using System.Net;
using System.Net.Http;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Semojum.Core.Integration;
using Semojum.Desktop.Dialogs;

namespace Semojum.Desktop.Services;

internal static class WindowsUpdateSafetyChecks
{
    private sealed class Handler(string latest = "99.0.0", bool offline = false) : HttpMessageHandler
    {
        internal int VersionRequests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/releases/latest")
            {
                if (request.Headers.Authorization?.Scheme != "Bearer" ||
                    request.RequestUri.Query != "?platform=WINDOWS&channel=STABLE&currentVersion=" + MainWindow.CurrentVersion.ToString(3))
                    throw new InvalidOperationException("The signed-in update request lost its account or version.");
                VersionRequests++;
                if (offline) throw new HttpRequestException("fixture offline");
            }
            var result = path.EndsWith("/login") ? "{\"accessToken\":\"a\",\"refreshToken\":\"r\",\"role\":\"ROLE_USER\"}"
                : path == "/api/releases/latest" ? JsonSerializer.Serialize(new { version = latest, minSupportedVersion = "0.0.1", fileUrl = "https://fixture.invalid/update.zip" })
                : path.EndsWith("/jobs/active") ? "[]" : throw new InvalidOperationException("Unexpected fixture request: " + path);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"isSuccess\":true,\"result\":" + result + "}", Encoding.UTF8, "application/json") });
        }
    }
    private static async Task CheckPackageChecksumAsync(Action<bool,string> check)
    {
        var root = Directory.CreateTempSubdirectory("nuridot-package-qa-").FullName;
        try
        {
            var archive = Path.Combine(root, "valid.zip");
            var assembly = typeof(MainWindow).Assembly;
            using (var zip = System.IO.Compression.ZipFile.Open(archive, System.IO.Compression.ZipArchiveMode.Create))
            {
                foreach (var name in new[] { "Semojum.Mockup.exe", "Semojum.Mockup.runtimeconfig.json", "engine/semojum-engine.exe" })
                    using (var writer = new StreamWriter(zip.CreateEntry(name).Open())) writer.Write("fixture");
                using var entry = zip.CreateEntry("Semojum.Mockup.dll").Open();
                using var input = File.OpenRead(assembly.Location);
                await input.CopyToAsync(entry);
            }
            var verified = Path.Combine(root, "verified");
            try
            {
                await PortableUpdate.StageAsync(archive, verified, assembly.GetName().Version!, new string('0', 64), default);
                throw new InvalidOperationException("A mismatched package checksum was accepted");
            }
            catch (InvalidDataException ex) { check(ex.Message.Contains("SHA-256") && !Directory.Exists(verified), "A checksum mismatch stops the ZIP before any version directory is staged"); }
            var checksum = (await PortableUpdate.HashAsync(archive)).ToLowerInvariant();
            await PortableUpdate.StageAsync(archive, verified, assembly.GetName().Version!, checksum, default);
            check(File.Exists(Path.Combine(verified, "pending.json")), "A matching ZIP checksum stages the verified Windows assembly");
        }
        finally { Directory.Delete(root, true); }
    }

    internal static async Task RunAsync(Action<bool, string> check)
    {
        await CheckPackageChecksumAsync(check);
        using var http = new HttpClient(new Handler());
        var api = new SemojumApiClient(http); await api.LoginAsync("editor", "fixture");
        var window = new MainWindow(api); window.Show();
        var calls = 0;
        var confirmations = new List<string>();
        window.UpdateConfirmationForChecks = question => { confirmations.Add(question); return MessageBoxResult.No; };
        window.UpdateDownloadForChecks = (_, _, _) => { calls++; throw new IOException("fixture update failure"); };
        try
        {
            var model = window.Session; model.ActiveBlock!.Source = "저장하지 않은 업데이트 검증 내용";
            var startup = typeof(MainWindow).GetMethod("StartAccountAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            await (Task)startup.Invoke(window, null)!;
            bool Preserved() => ReferenceEquals(model, window.Session) && model.IsDirty && model.ActiveBlock!.Source == "저장하지 않은 업데이트 검증 내용";
            check(confirmations.Count == 1 && confirmations[0].Contains("99.0.0"), "Startup prompts once with the available version before the user opens version information");
            check(calls == 0 && Preserved() && model.Status.Contains("새 버전"), "Postponing the startup update preserves unsaved edits without downloading");
            await window.ExecuteAsync("version");
            check(confirmations.Count == 2 && calls == 0 && Preserved(), "Manual version check remains available after postponing the startup update");
            window.UpdateConfirmationForChecks = _ => MessageBoxResult.Yes;
            await window.ExecuteAsync("version");
            check(calls == 1 && Preserved() && model.Status.Contains("업데이트를 준비하지 못했습니다"),
                "A failed optional update reports the failure and keeps unsaved edits");
            window.UpdateDownloadForChecks = (_, _, _) => { calls++; return Task.CompletedTask; };
            await window.ExecuteAsync("version");
            check(calls == 2 && Preserved(), "Accepting an optional update requests a download without closing or changing the document");
        }
        finally { await window.ReleaseResourcesAsync(); window.PrepareSmokeShutdown(); window.Close(); }
        await CheckStartupChoicesAsync(check);
    }

    private static async Task CheckStartupChoicesAsync(Action<bool, string> check)
    {
        foreach (var (latest, automatic, offline, download, expectedPrompts) in new[]
        {
            ("99.0.0", true, false, false, 1),
            ("99.0.0", true, false, true, 1),
            (MainWindow.CurrentVersion.ToString(3), true, false, false, 0),
            ("99.0.0", false, false, false, 0),
            ("99.0.0", true, true, false, 0)
        })
        {
            var handler = new Handler(latest, offline);
            using var http = new HttpClient(handler);
            var api = new SemojumApiClient(http); await api.LoginAsync("editor", "fixture");
            var window = new MainWindow(api); window.Show();
            var downloads = 0; var prompts = 0; var validDialog = true;
            window.UpdateDownloadForChecks = (_, _, _) => { downloads++; return Task.CompletedTask; };
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
            timer.Tick += (_, _) =>
            {
                if (window.OwnedWindows.OfType<NoticeDialog>().FirstOrDefault(d => d.IsVisible) is not { } dialog) return;
                prompts++;
                validDialog &= dialog.MessageText == "새 버전 99.0.0이 있습니다." && dialog.DetailText.Contains(MainWindow.CurrentVersion.ToString(3))
                    && dialog.ActionButtons.Select(b => b.Content?.ToString()).SequenceEqual(new[] { "다운로드", "나중에" });
                if (validDialog) dialog.ActionButtons[download ? 0 : 1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                else dialog.Close();
            };
            try
            {
                window.AutoUpdateToggle.IsChecked = automatic;
                await window.ExecuteAsync("autoUpdate");
                timer.Start();
                await (Task)typeof(MainWindow).GetMethod("StartAccountAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null)!;
                check(prompts == expectedPrompts && validDialog && downloads == (download ? 1 : 0),
                    $"Startup popup offers download or later and honors the choice (latest={latest}, automatic={automatic}, offline={offline}, download={download})");
                check(handler.VersionRequests == (automatic ? 1 : 0), "Startup version request respects the automatic update setting");
                var before = window.SixDotToggle.IsChecked;
                await window.ExecuteAsync("sixDotMode");
                check(window.IsVisible && window.IsEnabled && window.SixDotToggle.IsChecked != before,
                    "Editor stays usable after the startup update decision or an unavailable version service");
            }
            finally { timer.Stop(); await window.ReleaseResourcesAsync(); window.PrepareSmokeShutdown(); window.Close(); }
        }
    }
}
