using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Semojum.Core;
using Semojum.Core.Integration;
using Semojum.Desktop.Dialogs;
using SharpVectors.Converters;

namespace Semojum.Desktop.Services;

internal static class WindowsVisualDescriptionDesignChecks
{
    internal static async Task RunAsync(string directory, Action<bool,string> check)
    {
        using var service = new SettingsFixture();
        using var http = new HttpClient(service);
        var api = new SemojumApiClient(http);
        await api.LoginAsync("fixture","fixture");
        var window = new MainWindow(smoke:true, testWithoutAccount:true, apiClient:api);
        window.Show();
        try
        {
            await Open(async dialog =>
            {
                var types = List(dialog);
                check(dialog.Title == "대체 초안 유형" && dialog.ActualWidth == 680 && dialog.ActualHeight == 442 &&
                    types.Items.Count == 5 && types.SelectedIndex == 0, "The ribbon opens the 680x442 two-column figure description settings");
                var input = Choice(dialog,"그림 생략");
                var origin = input.TranslatePoint(new(),dialog);
                check(origin == new Point(201,89) && input.ActualWidth == 454 && input.ActualHeight == 40,
                    $"The first description row is at {origin} and measures {input.ActualWidth}x{input.ActualHeight}");
                check(Items<TextBlock>(types).Where(t => t.Text.Contains('/')).Select(t => t.Text).SequenceEqual(["4/6","4/4","3/6","4/6","2/4"]),
                    "Initial enabled counts match the Figma for all five material types");
                check(Items<SvgViewbox>(dialog).Where(i => i.IsVisible).All(i => i.Drawings is { Bounds.IsEmpty:false }),
                    "The original close, reset and checkmark artwork renders");
                await WindowsSmoke.RenderAsync(dialog,Path.Combine(directory,"visual-types.png"));
                input.IsChecked = true;
                var preferred = input.ContextMenu!.Items.OfType<MenuItem>().Single();
                check(preferred.IsEnabled, "A selected description exposes its default-setting command");
                preferred.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                types.SelectedIndex = 1;
                dialog.UpdateLayout();
                Choice(dialog,"표 풀어쓰기").IsChecked = false;
                types.SelectedIndex = 0;
                dialog.UpdateLayout();
                check(Choice(dialog,"그림 생략").IsChecked == true && AutomationProperties.GetHelpText(Choice(dialog,"그림 생략")) == "현재 기본 설명",
                    "Switching material categories retains the selected choices and preferred description");
                Press(dialog,"확인");
            });
            var saved = window.Session.Options.VisualDescriptions!;
            check(service.Writes == 1 && saved["figure"].DefaultOption == "생략" && saved["table"].Options.Length == 3 &&
                service.Stored["visualDescriptions"]?["figure"]?["defaultOption"]?.GetValue<string>() == "생략" &&
                service.Stored["formId"]?.GetValue<string>() == "builtin:EXAM", "Confirm saves the selected default through the account API and applies it to the open document");
            var file = Path.Combine(directory,"visual-types.smj");
            await SmjFile.SaveAsync(file,SmjFile.Snapshot(window.Session));
            var reloaded = await SmjFile.LoadAsync(file);
            check(reloaded.Options.VisualDescriptions?["figure"].DefaultOption == "생략" && reloaded.Options.VisualDescriptions["table"].Options.Length == 3,
                "Reopening the SMJ restores the document description choices and default");
            await Open(dialog =>
            {
                var types = List(dialog);
                var omit = Choice(dialog,"그림 생략");
                check(omit.IsChecked == true && AutomationProperties.GetHelpText(omit) == "현재 기본 설명", "Reopening settings loads the saved preferred option");
                omit.IsChecked = false;
                check(AutomationProperties.GetHelpText(Choice(dialog,"그림 짧은 제목")) == "현재 기본 설명", "Removing the preferred choice picks the first remaining enabled description");
                foreach (var choice in Items<CheckBox>(dialog).ToArray()) choice.IsChecked = false;
                types.SelectedIndex = 2;
                Press(dialog,"확인");
                dialog.UpdateLayout();
                check(dialog.IsVisible && types.SelectedIndex == 0 && Items<TextBlock>(dialog).Any(t => t.IsVisible && t.Text.Contains("하나 이상")),
                    "Saving locates a category left empty even when another category is open");
                var restore = Items<Button>(dialog).Single(b => AutomationProperties.GetName(b) == "기본값 복원");
                restore.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                types.SelectedIndex = 1;
                dialog.UpdateLayout();
                check(Choice(dialog,"표 풀어쓰기").IsChecked == true, "Restore resets inactive categories as well as the selected one");
                Press(dialog,"취소");
                return Task.CompletedTask;
            });
            check(service.Writes == 1 && window.Session.Options.VisualDescriptions!["figure"].DefaultOption == "생략", "Cancel after reset leaves account and document choices unchanged");
        }
        finally { await window.ReleaseResourcesAsync(); window.PrepareSmokeShutdown(); window.Close(); }

        Task Open(Func<VisualDescriptionDialog,Task> inspect) => WindowsSmoke.WithModal(() => window.ExecuteAsync("visualDescriptions"),inspect);
    }

    private static ListBox List(Window window) => Items<ListBox>(window).Single(b => AutomationProperties.GetName(b) == "자료 유형");
    private static CheckBox Choice(Window window,string name) => Items<CheckBox>(window).Single(b => AutomationProperties.GetName(b) == name);
    private static void Press(Window window,string label) => Items<Button>(window).Single(b => Equals(b.Content,label)).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    private static IEnumerable<T> Items<T>(DependencyObject root) where T:DependencyObject
    {
        for (int i=0; i<VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root,i);
            if (child is T item) yield return item;
            foreach (var nested in Items<T>(child)) yield return nested;
        }
    }

    private sealed class SettingsFixture : HttpMessageHandler
    {
        internal JsonObject Stored { get; private set; } = new() { ["formId"]="builtin:EXAM", ["pageNumberLine"]="odd" };
        internal int Writes { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            JsonNode result = new JsonObject();
            switch (request.RequestUri!.AbsolutePath)
            {
                case "/api/auth/login":
                    result = new JsonObject { ["accessToken"]="fixture", ["refreshToken"]="fixture", ["role"]="ROLE_USER", ["alias"]="검수자" };
                    break;
                case "/api/users/braille-options":
                    if (request.Method == HttpMethod.Put)
                    {
                        Stored = JsonNode.Parse(await request.Content!.ReadAsStringAsync(token))!.AsObject();
                        Writes++;
                    }
                    result = Stored;
                    break;
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { isSuccess=true,result }), Encoding.UTF8,"application/json") };
        }
    }
}
