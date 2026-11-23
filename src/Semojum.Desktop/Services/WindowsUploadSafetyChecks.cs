using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Semojum.Core.Integration;
using Semojum.Desktop.Dialogs;

namespace Semojum.Desktop.Services;

internal static class WindowsUploadSafetyChecks
{
    internal static async Task RunAsync(string outputDirectory, Action<bool, string> check)
    {
        var folder = Path.Combine(Path.GetTempPath(), "semojum-upload-safety-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            var a = Path.Combine(folder, "a"); var b = Path.Combine(folder, "b"); Directory.CreateDirectory(a); Directory.CreateDirectory(b);
            var first = Path.Combine(a, "same.png"); var duplicate = Path.Combine(b, "same.png"); var other = Path.Combine(b, "other.png");
            await File.WriteAllBytesAsync(first, [1]); await File.WriteAllBytesAsync(duplicate, [2]); await File.WriteAllBytesAsync(other, [3]);
            using var handler = new Fixture(); using var http = new HttpClient(handler); var api = new SemojumApiClient(http); await api.LoginAsync("fixture", "fixture");
            var window = new MainWindow(api); window.Show();
            var extra = Path.Combine(a, "extra.png");
            var matchingEstimate = false;
            var errors = new List<string>(); var settingsShown = 0; var orderShown = 0; var stage = 0;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
            timer.Tick += (_, _) =>
            {
                var notice = Application.Current.Windows.OfType<NoticeDialog>().FirstOrDefault(n => n.IsVisible && n.Owner == window);
                if (notice is not null) { errors.Add(notice.DetailText); notice.ActionButtons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return; }
                var settings = Application.Current.Windows.OfType<OpenDocumentDialog>().FirstOrDefault(d => d.IsVisible && d.Owner == window);
                if (settings is not null && stage == 3)
                {
                    stage = 4; settingsShown++;
                    settings.Closed += (_, _) => { using var file = new FileStream(other, FileMode.Open, FileAccess.Write, FileShare.ReadWrite); file.SetLength(JobClient.MaximumUploadBytes + 1); };
                    Descendants(settings).OfType<Button>().Single(button => Equals(button.Content, "변환 시작")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    return;
                }
                var order = Application.Current.Windows.OfType<ImageOrderDialog>().FirstOrDefault(d => d.IsVisible && d.Owner == window);
                if (order is not null && stage == 5)
                {
                    var browser = Descendants(order).OfType<ImageUploadBrowser>().Single();
                    var add = Descendants(browser.Files).OfType<CheckBox>().FirstOrDefault(c => c.DataContext is ImageUploadBrowser.FileRow r && r.Path == extra);
                    if (add is null) return;
                    stage = 6;
                    add.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
                    browser.Files.SelectedItem = browser.Files.Items.Cast<ImageUploadBrowser.FileRow>().Single(r => r.Path == other);
                    ImageUploadBrowser.MoveUp.Execute(null, browser.Files);
                    Descendants(order).OfType<Button>().Single(b => Equals(b.Content, "올리기")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    return;
                }
                if (settings is not null && stage == 6)
                {
                    stage = 7;
                    matchingEstimate = settings.Paths.SequenceEqual([other, first, extra]) && settings.FileNames.Text.EndsWith(" · 3쪽")
                        && Descendants(settings).OfType<TextBlock>().Any(t => t.Text == "300 크레딧");
                    Descendants(settings).OfType<Button>().Single(b => Equals(b.Content, "변환 시작")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    return;
                }
                if (order is not null && stage == 2)
                {
                    stage = 3; orderShown++;
                    Descendants(order).OfType<Button>().Single(button => Equals(button.Content, "올리기")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
            };
            try
            {
                await window.ExecuteAsync("new"); var model = window.Session; model.ActiveBlock!.Source = "미저장 원문"; model.ActiveBlock.Braille = "⠁⠃"; model.ActiveBlock.HasManualBraille = true;
                var tabs = window.DocumentTabs.Items.Count; timer.Start();
                window.FilePicker = _ => [first, duplicate]; stage = 1;
                window.OriginalUpload.UploadArea.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await WaitFor(() => errors.Count >= 1);
                check(errors[0].Contains("이름이 같은") && handler.Uploads == 0, "Duplicate image filenames show an error before upload");
                check(ReferenceEquals(model, window.Session) && model.IsDirty && model.ActiveBlock!.Source == "미저장 원문" && model.ActiveBlock.Braille == "⠁⠃" && window.DocumentTabs.Items.Count == tabs,
                    "Duplicate image rejection preserves the unsaved document and manual braille");
                window.FilePicker = _ => [first, other]; stage = 2;
                window.OriginalUpload.UploadArea.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await WaitFor(() => errors.Count >= 2);
                check(settingsShown == 1 && orderShown == 1 && errors[1].Contains("100 MB") && handler.Uploads == 0,
                    "A file enlarged after settings is rejected before upload with a clear error");
                check(ReferenceEquals(model, window.Session) && model.IsDirty && model.ActiveBlock!.Source == "미저장 원문" && model.ActiveBlock.Braille == "⠁⠃" && window.DocumentTabs.Items.Count == tabs,
                    "Read-time size rejection preserves the unsaved document and manual braille");
                await File.WriteAllBytesAsync(other, [3]); await File.WriteAllBytesAsync(extra, [4]); stage = 5;
                window.OriginalUpload.UploadArea.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await WaitFor(() => errors.Count >= 3);
                check(matchingEstimate, "The conversion options count and price the final ordered selection from the image browser");
                check(handler.Uploads == 1 && handler.Uploaded.SequenceEqual(new[] { ("other.png", (byte)3), ("same.png", (byte)1), ("extra.png", (byte)4) }),
                    "The editor sends the chosen images in page order with the matching bytes in its multipart job request");
            }
            finally { timer.Stop(); await window.ReleaseResourcesAsync(); window.PrepareSmokeShutdown(); window.Close(); }
        }
        finally { Directory.Delete(folder, true); }
    }
    private static async Task WaitFor(Func<bool> condition)
    {
        for (var i = 0; i < 150 && !condition(); i++) await Task.Delay(20);
        if (!condition()) throw new TimeoutException("Upload safety dialog did not complete");
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    private sealed class Fixture : HttpMessageHandler
    {
        public int Uploads { get; private set; }
        public List<(string Name, byte Marker)> Uploaded { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path == "/api/jobs")
            {
                Uploads++;
                foreach (var part in (MultipartFormDataContent)request.Content!)
                    if (part.Headers.ContentDisposition?.FileName is { } name)
                        Uploaded.Add((name.Trim('"'), (await part.ReadAsByteArrayAsync(cancellationToken)).Single()));
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("""{"isSuccess":false,"code":"QA","message":"시험 업로드 확인"}""", Encoding.UTF8, "application/json") };
            }
            var result = path.EndsWith("/login") ? "{\"accessToken\":\"fixture\",\"refreshToken\":\"fixture\",\"role\":\"ROLE_USER\"}" : path.EndsWith("/braille-forms") ? "{\"items\":[]}" : path.EndsWith("/proof-pricing") ? """{"pageCredits":{"PAGE_LAYOUT_TEXT":100,"PAGE_LAYOUT_VISUAL":500},"models":[{"code":"MINERU","extraCredits":0}]}""" : "{}";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"isSuccess\":true,\"result\":" + result + "}", Encoding.UTF8, "application/json") };
        }
    }
}
