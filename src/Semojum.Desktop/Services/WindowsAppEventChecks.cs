using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Semojum.Core;
using Semojum.Core.Integration;

namespace Semojum.Desktop.Services;

internal static class WindowsAppEventChecks
{
    private sealed class MemoryStore : IAppEventStore
    {
        public EventQueue Queue = new("", []);
        public EventQueue Load() => Queue;
        public void Save(EventQueue value) => Queue = value;
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) => Task.FromResult(send(r)); }
    private static HttpResponseMessage Ok(object result) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { isSuccess = true, code = "COMMON2000", result }), Encoding.UTF8, "application/json") };
    internal static async Task RunAsync(string directory, Action<bool, string> check)
    {
        var folder = Path.Combine(directory, "events"); Directory.CreateDirectory(folder);
        var context = Guid.NewGuid(); var store = new MemoryStore();
        using var http = new HttpClient(new Handler(r => r.RequestUri!.AbsolutePath switch
        {
            "/api/auth/login" => Ok(new { accessToken = "test", refreshToken = "refresh", role = "ROLE_USER" }),
            "/api/events/config" => Ok(new { enabled = true, collection_context_id = context, enabled_event_names = AppEvents.Names }),
            "/api/jobs/job-1/save" => Ok(new { savedPages = 1, failedPages = Array.Empty<int>(), savedAt = "2026-11-19T12:00:00", jobId = "job-1" }),
            _ => throw new InvalidOperationException("Unexpected event fixture API: " + r.RequestUri.AbsolutePath)
        }));
        var api = new SemojumApiClient(http); await api.LoginAsync("owner", "test");
        var events = new AppEvents(api, store, "1.0.5"); await events.RefreshAsync();
        var model = new DocumentSession { JobId = "job-1", OwnerLoginId = "owner", IsTextJob = false };
        var block = new EditorBlock { Id = 1, Page = 1, ServerId = "block-1", Source = "가나다", AiBaseline = new("baseline-1", "가나다") };
        block.ApplyTranslatedBraille("⠁⠃"); block.EditBraille("⠁⠉"); model.Add(block);
        model.ServerPages[1] = [new("block-1", true)]; model.PageStatuses[1] = "COMPLETED";
        var path = Path.Combine(folder, "document.smj"); await SmjFile.SaveAsync(path, SmjFile.Snapshot(model));
        var window = new MainWindow(api, startEmpty: true); window.Show();
        typeof(MainWindow).GetField("appEvents", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, events);
        try
        {
            await window.OpenAsync(path);
            check(store.Queue.Events.Count(e => e.EventName == "document_open_result") == 1, "A real local open records exactly one successful open");
            await window.ExecuteAsync("save");
            var saved = store.Queue.Events.Last(e => e.EventName == "save_result"); var savedId = saved.Properties.GetProperty("saved_revision_id").GetGuid();
            check(saved.Properties.GetProperty("result").GetString() == "success", "Save event follows a confirmed local and server save");
            check(store.Queue.Events.Count(e => e.EventName == "ai_revision_summary") == 2, "First manual save records separate AI and engine summaries");
            await window.ExecuteAsync("save");
            check(store.Queue.Events.Last(e => e.EventName == "save_result").Properties.GetProperty("saved_revision_id").GetGuid() == savedId
                && store.Queue.Events.Count(e => e.EventName == "ai_revision_summary") == 2, "Unchanged manual save keeps its revision and does not duplicate summaries");
            await window.ExecuteAsync("completeWork");
            var completed = store.Queue.Events.Single(e => e.EventName == "job_completed");
            check(completed.Properties.GetProperty("saved_revision_id").GetGuid() == savedId
                && store.Queue.Events.Count(e => e.EventName == "ai_revision_summary") == 4, "Completion references the saved revision and includes its full summary snapshot");
            await window.ExecuteAsync("completeWork");
            check(store.Queue.Events.Count(e => e.EventName == "job_completed") == 1, "Repeated completion cannot double-count a work session");
            window.Session.Blocks[0].EditBraille("⠙"); await window.ExecuteAsync("save");
            var revised = store.Queue.Events.Last(e => e.EventName == "save_result");
            check(revised.WorkSessionId != completed.WorkSessionId && revised.DocumentId == completed.DocumentId
                && revised.WorkRevision == completed.WorkRevision + 1, "Editing after completion starts a new session for the same document");
            var export = typeof(MainWindow).GetMethod("ExportAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            await (Task)export.Invoke(window, [Path.Combine(folder, "export.txt"), 2, false])!;
            check(store.Queue.Events.Last(e => e.EventName == "export_result").Properties.GetProperty("format").GetString() == "text"
                && File.Exists(Path.Combine(folder, "export.txt")), "Export success is recorded after the output file exists");
            var serialized = JsonSerializer.Serialize(store.Queue.Events, new JsonSerializerOptions(AppEvents.Json) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            check(!serialized.Contains("가나다") && !serialized.Contains("⠁") && !serialized.Contains("document.smj") && !serialized.Contains("export.txt"), "Event payloads contain neither document text nor file names");
        }
        finally { window.PrepareSmokeShutdown(); window.Close(); await window.ReleaseResourcesAsync(); }

        var encryptedPath = Path.Combine(folder, "encrypted");
        using (var encrypted = new WindowsAppEventStore(encryptedPath)) encrypted.Save(store.Queue);
        var disk = File.ReadAllBytes(Directory.GetFiles(encryptedPath, "*.queue").Single());
        check(!Encoding.UTF8.GetString(disk).Contains("work_session_id") && !Encoding.UTF8.GetString(disk).Contains("owner"), "DPAPI encrypts the on-disk event queue");
        using (var reopened = new WindowsAppEventStore(encryptedPath))
            check(reopened.Load()!.Events.Length == store.Queue.Events.Length, "Encrypted queue survives reopening under the same Windows user");
    }
}
