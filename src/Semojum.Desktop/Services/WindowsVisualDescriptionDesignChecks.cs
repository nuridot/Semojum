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
using Semojum.Feature.Settings;
using SharpVectors.Converters;

namespace Semojum.Desktop.Services;

internal static class WindowsVisualDescriptionDesignChecks
{
    internal static async Task RunLiveAsync(string origin, string directory, Action<bool, string> check)
    {
        var endpoint = new Uri(origin);
        if (!endpoint.IsLoopback) throw new InvalidOperationException("로컬 시험 서버만 사용할 수 있습니다.");
        using var http = new HttpClient();
        var api = new SemojumApiClient(http, endpoint);
        await api.LoginAsync(Environment.GetEnvironmentVariable("VISUAL_CATALOG_QA_LOGIN")!,
            Environment.GetEnvironmentVariable("VISUAL_CATALOG_QA_PASSWORD")!);
        var settings = new SettingsClient(api);
        var catalog = await settings.VisualOptionsAsync(default) ?? throw new InvalidDataException("설명 유형 목록이 없습니다.");
        var window = new MainWindow(smoke: true, testWithoutAccount: true, apiClient: api);
        window.Show();
        try
        {
            await WindowsSmoke.WithModal(() => window.ExecuteAsync("visualDescriptions"), async (VisualDescriptionDialog dialog) =>
            {
                var types = List(dialog);
                string[] keys = ["figure", "table", "chart", "diagram", "cartoon"];
                for (int i = 0; i < keys.Length; i++)
                {
                    types.SelectedIndex = i;
                    var choices = Items<CheckBox>(dialog).ToArray();
                    check(choices.Select(c => c.Content as string).SequenceEqual(catalog.Choices[keys[i]].Select(c => c.Label)),
                        $"The {keys[i]} list shows the {choices.Length} labels in catalog order");
                    foreach (var choice in choices) choice.IsChecked = true;
                }
                types.SelectedIndex = 0;
                Items<ComboBox>(dialog).Single().SelectedValue = 6;
                types.SelectedIndex = 2;
                Items<ComboBox>(dialog).Single().SelectedValue = 12;
                types.SelectedIndex = 4;
                Items<ComboBox>(dialog).Single().SelectedValue = 11;
                types.SelectedIndex = 3;
                var last = Items<CheckBox>(dialog).Last();
                last.BringIntoView();
                await dialog.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
                dialog.UpdateLayout();
                var position = last.TranslatePoint(new Point(), dialog);
                check(last.IsVisible && position.Y >= 0 && position.Y + last.ActualHeight <= dialog.ActualHeight,
                    "The tenth diagram option scrolls into the visible window");
                Items<ComboBox>(dialog).Single().SelectedValue = 13;
                await WindowsSmoke.RenderElementAsync(dialog, Path.Combine(directory, "diagram.png"));
                types.SelectedIndex = 1;
                Items<ComboBox>(dialog).Single().SelectedValue = 5;
                Press(dialog, "확인");
            });
            var saved = window.Session.Options.VisualOptions!;
            check(saved["image"].DefaultOption == 6 && saved["chart_graph"].DefaultOption == 12
                && saved["cartoon"].DefaultOption == 11 && saved["diagram"].DefaultOption == 13
                && saved["table"].DefaultOption == 5, "Account selections reached the open document with canonical keys and IDs");
            await WindowsSmoke.WithModal(() => window.ExecuteAsync("visualDescriptions"), (VisualDescriptionDialog dialog) =>
            {
                List(dialog).SelectedIndex = 1;
                check(Equals(Items<ComboBox>(dialog).Single().SelectedValue, 5),
                    "Reopening settings restores the numbered table default from the server");
                Press(dialog, "취소");
                return Task.CompletedTask;
            });
            var smj = Path.Combine(directory, "selected.smj");
            await SmjFile.SaveAsync(smj, SmjFile.Snapshot(window.Session));
            var reopened = await SmjFile.LoadAsync(smj);
            check(reopened.Options.VisualOptions!["diagram"].DefaultOption == 13, "SMJ preserves catalog IDs");
            var basis = (await settings.FormsAsync()).First(f => f.Builtin);
            var formOptions = basis.Options with { VisualOptions = new() { ["table"] = new([2, 3], 3) } };
            var form = await settings.SaveFormAsync(null, "대체 초안 QA", basis.FormId, basis.Layout, formOptions);
            check(form.Options.VisualOptions!["table"].Options.SequenceEqual([2, 3]), "Creating a form stores its numbered options");
            var forms = await settings.FormsAsync();
            var editor = new FormEditDialog(settings, forms, form, form.Layout, form.Options, form.BaseFormId) { Owner = window };
            await WindowsSmoke.WithModal(() => { editor.ShowDialog(); return Task.CompletedTask; }, async (FormEditDialog visible) =>
            {
                await WindowsSmoke.WithModal(() => { Press(visible, "대체 초안 유형"); return Task.CompletedTask; }, (VisualDescriptionDialog dialog) =>
                {
                    var image = catalog.Choices["figure"];
                    foreach (var choice in Items<CheckBox>(dialog))
                        choice.IsChecked = Equals(choice.Content, image.Single(c => c.Option == 6).Label);
                    Items<ComboBox>(dialog).Single().SelectedValue = 6;
                    Items<ComboBox>(dialog).Single().SelectedValue = 0;
                    List(dialog).SelectedIndex = 1;
                    var option2 = catalog.Choices["table"].Single(c => c.Option == 2);
                    Items<CheckBox>(dialog).Single(c => Equals(c.Content, option2.Label)).IsChecked = false;
                    Press(dialog, "확인");
                    return Task.CompletedTask;
                });
                var saveButton = Items<Button>(visible).Single(b => Equals(b.Content, "저장"));
                for (int i = 0; i < 100 && !saveButton.IsEnabled; i++) await Task.Delay(50);
                Press(visible, "저장");
                for (int i = 0; i < 100 && visible.IsVisible; i++) await Task.Delay(50);
                check(visible.Saved is not null, visible.Saved is not null ? "The form editor saves its own draft choices"
                    : "양식 저장 실패: " + string.Join(" ", Items<TextBlock>(visible).Select(t => t.Text)));
            });
            var listed = (await settings.FormsAsync()).Single(f => f.FormId == form.FormId).Options.VisualOptions!;
            check(listed["table"].Options.SequenceEqual([3]) && listed["image"].DefaultOption == 0,
                "A form edit survives listing with AI default zero");

            var jobs = new JobClient(api);
            var file = new UploadFile("대체초안.txt", Encoding.UTF8.GetBytes("대체 초안 연동 확인"));
            var uploadDialog = new OpenDocumentDialog(await settings.FormsAsync(), await settings.DefaultsAsync());
            check(uploadDialog.FormId is null && uploadDialog.Settings.Options.VisualOptions!["table"].DefaultOption == 5,
                "The upload dialog selects account preferences when the account has its own draft choices");
            var fromAccount = await jobs.UploadAsync([file], uploadDialog.Settings, formId: uploadDialog.FormId);
            var model = new DocumentSession { JobId = fromAccount.JobId, OwnerLoginId = api.Account!.LoginId };
            await jobs.ReadSettingsAsync(model);
            check(model.Options.VisualOptions!["table"].DefaultOption == 5, "An upload without a form inherits the account setting");
            uploadDialog.Forms.SelectedItem = uploadDialog.Forms.Items.Cast<BrailleForm>().Single(f => f.FormId == form.FormId);
            var fromForm = await jobs.UploadAsync([file], uploadDialog.Settings, formId: uploadDialog.FormId);
            uploadDialog.Close();
            model.JobId = fromForm.JobId;
            await jobs.ReadSettingsAsync(model, uploadSettings: window.Session);
            check(model.Options.VisualOptions!["table"].DefaultOption == 3 && model.Options.VisualOptions["image"].Options.SequenceEqual([6]),
                "Opening a new upload uses the server's effective form options");
            model.Options = model.Options with { VisualOptions = new() { ["table"] = new([], 0) } };
            model.IsTextJob = true;
            model.Blocks.Add(new EditorBlock { Id = 1, Page = 1, Source = "대체 초안 연동 확인" });
            await jobs.SaveAsync(SmjFile.Snapshot(model));
            await jobs.ReadSettingsAsync(model);
            check(!model.Options.VisualOptions!.ContainsKey("table") && model.Options.VisualOptions["image"].Options.SequenceEqual([6]),
                "Saving a job clears one category and keeps the omitted category on reopening");
            check((await settings.DefaultsAsync()).Options.VisualOptions!["table"].DefaultOption == 5,
                "Editing the form and job leaves the account default intact");
        }
        finally { await window.ReleaseResourcesAsync(); window.PrepareSmokeShutdown(); window.Close(); }

    }

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
                case "/api/users/visual-options":
                    return new HttpResponseMessage(HttpStatusCode.NotFound)
                    { Content = new StringContent("""{"isSuccess":false,"code":"NOT_FOUND","message":"이전 서버"}""", Encoding.UTF8, "application/json") };
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { isSuccess=true,result }), Encoding.UTF8,"application/json") };
        }
    }
}
