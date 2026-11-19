using System.Net;
using System.Text;
using System.Text.Json;
using Semojum.Core.Integration;

internal static class AppEventChecks
{
    public static IEnumerable<(string Name, Func<Task> Run)> Tests()
    {
        yield return ("App collection is gated by config, account, context and durable queue persistence", Gating);
        yield return ("Event retry preserves IDs/payload across transient errors and app restart", Retry);
        yield return ("Malformed acknowledgements retain events and revoked contexts purge every queued item", Acknowledgements);
        yield return ("Event batches split after 413 and never exceed the 100 item limit", Batch);
        yield return ("AI comparison uses NFC code points, deterministic edit counts and explicit missing baselines", Comparison);
        yield return ("Activity intervals split at idle, focus and suspension boundaries without inventing active time", Activity);
    }
    private sealed class MemoryStore : IAppEventStore
    {
        public EventQueue? Queue;
        public bool Fail;
        public EventQueue? Load() => Queue;
        public void Save(EventQueue value) { if (Fail) throw new IOException("full"); Queue = value; }
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken ct) => send(message); }
    private static HttpResponseMessage Ok(object data) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { isSuccess = true, code = "COMMON2000", result = data }), Encoding.UTF8, "application/json") };
    private static HttpResponseMessage Tokens() => Ok(new { accessToken = "test", refreshToken = "refresh", role = "ROLE_USER" });
    private static object Props() => new { operation_id = Guid.NewGuid(), retry_of_operation_id = (Guid?)null, result = "success", error_code = (string?)null, duration_ms = 5 };
    private static HttpResponseMessage Ack(JsonElement events, string status = "accepted", string? code = null) => Ok(new { results = events.EnumerateArray().Select((e, i) => new { index = i, event_id = e.GetProperty("event_id").GetString(), status, code }).ToArray() });
    private static async Task<JsonElement> Events(HttpRequestMessage request) => JsonDocument.Parse(await request.Content!.ReadAsStringAsync()).RootElement.GetProperty("events").Clone();
    private static async Task Gating()
    {
        var context = Guid.NewGuid(); var enabled = false; var store = new MemoryStore(); var posted = 0;
        using var http = new HttpClient(new Handler(async r =>
        {
            if (r.RequestUri!.AbsolutePath.EndsWith("login")) return Tokens();
            if (r.RequestUri.AbsolutePath.EndsWith("config")) return Ok(new { enabled, collection_context_id = enabled ? context : (Guid?)null, enabled_event_names = enabled ? AppEvents.Names : [] });
            posted++; return Ack(await Events(r));
        }));
        var api = new SemojumApiClient(http); await api.LoginAsync("first", "test");
        var client = new AppEvents(api, store, "1.0.5"); var session = new AppEventSession(Guid.NewGuid(), 1);
        Check(!client.Record(session, "document_open_result", null, Props()), "Collected before config");
        await client.RefreshAsync(); Check(!client.Record(session, "document_open_result", null, Props()), "Collected while disabled");
        enabled = true; await client.RefreshAsync();
        store.Fail = true; Check(!client.Record(session, "document_open_result", null, Props()) && session.EventSeq == 0, "Advanced sequence without persistence");
        store.Fail = false; Check(client.Record(session, "document_open_result", null, Props()) && session.EventSeq == 1, "Enabled event missing");
        await api.LoginAsync("second", "test"); await client.FlushAsync(); Check(posted == 0 && client.ContextId is null, "Old account queue crossed login");
        await client.RefreshAsync(); Check(store.Queue!.Events.Length == 0 && store.Queue.Owner == "second", "Old account queue retained");
        var old = new AppEventSession(Guid.NewGuid(), 1); client.Record(old, "document_open_result", null, Props());
        context = Guid.NewGuid(); await client.RefreshAsync();
        Check(store.Queue.Events.Length == 0 && !client.Record(old, "save_result", null, Props()), "Old collection context was reused");
    }
    private static async Task Retry()
    {
        var context = Guid.NewGuid(); var store = new MemoryStore(); var attempts = new List<string>(); var fail = true;
        using var http = new HttpClient(new Handler(async r =>
        {
            if (r.RequestUri!.AbsolutePath.EndsWith("login")) return Tokens();
            if (r.RequestUri.AbsolutePath.EndsWith("config")) return Ok(new { enabled = true, collection_context_id = context, enabled_event_names = AppEvents.Names });
            attempts.Add(await r.Content!.ReadAsStringAsync());
            if (fail) throw new HttpRequestException("offline");
            return Ack(await Events(r), "duplicate");
        }));
        var api = new SemojumApiClient(http); await api.LoginAsync("first", "test");
        var client = new AppEvents(api, store, "1.0.5"); await client.RefreshAsync();
        client.Record(new(Guid.NewGuid(), 1), "document_open_result", null, Props());
        try { await client.FlushAsync(); } catch (HttpRequestException) { }
        Check(store.Queue!.Events.Length == 1, "Transient failure lost event");
        var freshApi = new SemojumApiClient(http);
        fail = false; var reopened = new AppEvents(freshApi, store, "1.0.6");
        await reopened.RefreshAsync();
        Check(store.Queue.Events.Length == 1 && reopened.ContextId is null, "Startup before login erased the previous process queue");
        await freshApi.LoginAsync("first", "test"); await reopened.RefreshAsync(); await reopened.FlushAsync();
        Check(attempts[0] == attempts[1] && store.Queue.Events.Length == 0, "Retry changed payload or did not ack");
    }
    private static async Task Acknowledgements()
    {
        var context = Guid.NewGuid(); var store = new MemoryStore(); var malformed = true;
        using var http = new HttpClient(new Handler(async r =>
        {
            if (r.RequestUri!.AbsolutePath.EndsWith("login")) return Tokens();
            if (r.RequestUri.AbsolutePath.EndsWith("config")) return Ok(new { enabled = true, collection_context_id = context, enabled_event_names = AppEvents.Names });
            return malformed ? Ok(new { results = new[] { new { index = 0, event_id = Guid.NewGuid(), status = "accepted", code = (string?)null } } }) : Ack(await Events(r), "rejected", "CONTEXT_REVOKED");
        }));
        var api = new SemojumApiClient(http); await api.LoginAsync("first", "test");
        var client = new AppEvents(api, store, "1.0.5"); await client.RefreshAsync();
        var work = new AppEventSession(Guid.NewGuid(), 1);
        for (var i = 0; i < 101; i++) client.Record(work, "document_open_result", null, Props());
        await client.FlushAsync(); Check(store.Queue!.Events.Length == 101, "Malformed ack removed events");
        malformed = false; await client.FlushAsync(); Check(store.Queue.Events.Length == 0 && client.ContextId is null, "Revocation left unbatched events queued");
    }
    private static async Task Batch()
    {
        var sizes = new List<int>(); var context = Guid.NewGuid(); var store = new MemoryStore();
        using var http = new HttpClient(new Handler(async r =>
        {
            if (r.RequestUri!.AbsolutePath.EndsWith("login")) return Tokens();
            if (r.RequestUri.AbsolutePath.EndsWith("config")) return Ok(new { enabled = true, collection_context_id = context, enabled_event_names = AppEvents.Names });
            var events = await Events(r); sizes.Add(events.GetArrayLength());
            if (sizes.Count == 1) return new(HttpStatusCode.RequestEntityTooLarge) { Content = new StringContent("{\"isSuccess\":false,\"code\":\"EVENT4130\",\"message\":\"large\"}") };
            return Ack(events);
        }));
        var api = new SemojumApiClient(http); await api.LoginAsync("first", "test");
        var client = new AppEvents(api, store, "1.0.5"); await client.RefreshAsync();
        var work = new AppEventSession(Guid.NewGuid(), 1);
        for (var i = 0; i < 101; i++) client.Record(work, "document_open_result", null, Props());
        await client.FlushAsync(); await client.FlushAsync(); await client.FlushAsync(); await client.FlushAsync();
        Check(sizes.SequenceEqual(new[] { 100, 50, 50, 1 }) && store.Queue!.Events.Length == 0, "Batch split lost or duplicated events");
    }
    private static Task Comparison()
    {
        var normalized = RevisionComparison.Compare("가\r\n😀", "가\n😀");
        Check(normalized.BaselineLength == 3 && normalized.EditDistance == 0, "Normalization counts UTF-16 instead of code points");
        var tie = RevisionComparison.Compare("ab", "ba");
        Check(tie.Substitutions == 2 && tie.Insertions == 0 && tie.Deletions == 0, "Tie order changed");
        var changed = RevisionComparison.Compare("가나다", "가라다마");
        Check(changed.Insertions == 1 && changed.Substitutions == 1 && changed.FinalLength == changed.BaselineLength + changed.Insertions - changed.Deletions, "Invalid edit counts");
        Check(RevisionComparison.Compare(null, "hello").BaselineLength is null, "Invented missing baseline");
        Check(RevisionComparison.Compare(new string('a', 3000), new string('b', 3000)).ComparisonStatus == "unsupported", "Unbounded comparison");
        return Task.CompletedTask;
    }
    private static Task Activity()
    {
        var now = DateTimeOffset.Parse("2026-11-19T00:00:00Z"); var intervals = new List<JsonElement>();
        void Emit(object value) => intervals.Add(JsonSerializer.SerializeToElement(value));
        var activity = new ActivityIntervals(now, 0, true);
        for (var n = 1; n <= 10; n++) activity.Update(now.AddSeconds(n * 30), n * 30000, true, false, false, Emit);
        activity.Update(now.AddSeconds(310), 310000, true, true, false, Emit);
        activity.Update(now.AddSeconds(315), 315000, false, false, false, Emit);
        activity.Update(now.AddSeconds(400), 400000, true, false, false, Emit);
        activity.Update(now.AddSeconds(405), 405000, true, false, true, Emit);
        Check(intervals[10].GetProperty("state").GetString() == "idle", "Idle began at heartbeat rather than 300 seconds");
        Check(intervals[11].GetProperty("duration_ms").GetInt64() == 5000, "Input did not restore active state");
        Check(intervals[12].GetProperty("state").GetString() == "unknown" && intervals[12].GetProperty("reason").GetString() == "resume_gap", "Suspension counted as active");
        Check(intervals[^1].GetProperty("reason").GetString() == "session_end", "Completion interval not closed");
        for (var i = 1; i < intervals.Count; i++) Check(intervals[i].GetProperty("mono_start_ms").GetInt64() == intervals[i - 1].GetProperty("mono_end_ms").GetInt64(), "Activity overlap/gap");
        return Task.CompletedTask;
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
