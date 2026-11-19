using System.Net;
using System.Text.Json;

namespace Semojum.Core.Integration;

public sealed record AppEvent(int SchemaVersion, Guid EventId, string EventName, string OccurredAt,
    Guid CollectionContextId, string AppVersion, string Platform, Guid AppInstanceId, Guid DocumentId,
    Guid WorkSessionId, long WorkRevision, long EventSeq, string? JobId, JsonElement Properties);
public sealed record EventQueue(string Owner, AppEvent[] Events);
public interface IAppEventStore
{
    EventQueue? Load();
    // Atomic, OS-encrypted replacement. A failure leaves the old queue intact.
    void Save(EventQueue queue);
}

public sealed class AppEventSession(Guid documentId, long workRevision)
{
    public Guid DocumentId { get; } = documentId;
    public Guid WorkSessionId { get; } = Guid.NewGuid();
    public long WorkRevision { get; } = Math.Max(1, workRevision);
    public long EventSeq { get; internal set; }
    public Guid? ContextId { get; internal set; }
    public bool Completed { get; set; }
}

/// <summary>Fail-closed collection, durable idempotent events and account-bound delivery. No document text enters this boundary.</summary>
public sealed class AppEvents
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    public static readonly string[] Names = ["document_open_result", "save_result", "export_result", "job_completed", "ai_revision_summary", "activity_state_changed"];
    private readonly object gate = new();
    private readonly SemaphoreSlim network = new(1, 1);
    private readonly SemojumApiClient api;
    private readonly IAppEventStore store;
    private readonly string version;
    private readonly TimeProvider time;
    private readonly Guid instance = Guid.NewGuid();
    private EventQueue queue = new("", []);
    private Guid? authSession, context;
    private string? role;
    private HashSet<string> enabled = [];
    private int batchSize = 100;
    public AppEvents(SemojumApiClient api, IAppEventStore store, string version, TimeProvider? time = null)
    {
        this.api = api; this.store = store; this.version = version; this.time = time ?? TimeProvider.System;
        try { queue = store.Load() ?? queue; } catch (Exception ex) when (ex is IOException or System.Security.Cryptography.CryptographicException or JsonException) { }
    }
    public Guid? ContextId { get { lock (gate) return IdentityMatches() ? context : null; } }
    public bool Allows(string name) { lock (gate) return IdentityMatches() && context.HasValue && enabled.Contains(name); }
    private bool IdentityMatches() => authSession is not null && api.SessionMarker == authSession
        && api.Account is { } account && account.LoginId == queue.Owner && account.Role == role;
    public static string Stamp(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);

    public void Disable()
    {
        lock (gate)
        {
            context = null; enabled.Clear(); authSession = null; role = null;
            Replace(new("", []));
        }
    }
    public void Suspend()
    {
        // Startup and an expired token are not consent revocation. Keep the encrypted queue until its owner signs in.
        lock (gate) { context = null; enabled.Clear(); authSession = null; role = null; }
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        await network.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var marker = api.SessionMarker; var account = api.Account;
            if (marker is null || account is null) { Suspend(); return; }
            if (account.Role == "ROLE_ADMIN") { Disable(); return; }
            lock (gate)
            {
                if (authSession != marker || role != account.Role) { context = null; enabled.Clear(); }
                if (queue.Owner != account.LoginId) Replace(new(account.LoginId, []));
            }
            var result = await api.SendAuthenticatedAsync(HttpMethod.Get, "/api/events/config", cancellationToken: ct, expectedSession: marker).ConfigureAwait(false);
            lock (gate)
            {
                if (api.SessionMarker != marker || api.Account?.LoginId != account.LoginId || api.Account.Role != account.Role) return;
                authSession = marker; role = account.Role;
                context = result.GetProperty("enabled").GetBoolean() && result.TryGetProperty("collection_context_id", out var id) && id.TryGetGuid(out var parsed) ? parsed : null;
                enabled = context is null ? [] : result.GetProperty("enabled_event_names").EnumerateArray().Select(n => n.GetString()!).Where(Names.Contains).ToHashSet();
                Replace(new(account.LoginId, queue.Events.Where(e => e.CollectionContextId == context && !Expired(e)).ToArray()));
            }
        }
        catch (SemojumApiException ex) when (ex.Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) { Suspend(); }
        finally { network.Release(); }
    }

    public Dictionary<string, object?>? AnalyticsContext(AppEventSession session)
    {
        lock (gate)
        {
            if (!IdentityMatches() || context is null || session.Completed || session.ContextId.HasValue && session.ContextId != context) return null;
            session.ContextId = context;
            return new() { ["collection_context_id"] = context, ["document_id"] = session.DocumentId,
                ["work_session_id"] = session.WorkSessionId, ["work_revision"] = session.WorkRevision, ["app_version"] = version };
        }
    }

    public bool Record(AppEventSession session, string name, string? jobId, object properties, DateTimeOffset? at = null)
    {
        lock (gate)
        {
            if (!Allows(name) || session.Completed || session.ContextId.HasValue && session.ContextId != context) return false;
            var data = new AppEvent(1, Guid.NewGuid(), name, Stamp(at ?? time.GetUtcNow()), context!.Value, version, "windows", instance,
                session.DocumentId, session.WorkSessionId, session.WorkRevision, session.EventSeq + 1, jobId, JsonSerializer.SerializeToElement(properties, Json));
            // Refuse growth rather than silently dropping unacknowledged events or leaking raw payloads into diagnostics.
            var current = queue.Events.Where(e => !Expired(e)).ToArray();
            if (current.Length >= 10000 || JsonSerializer.SerializeToUtf8Bytes(data, Json).Length > 128 * 1024) return false;
            if (!Replace(queue with { Events = [.. current, data] })) return false;
            session.EventSeq++; session.ContextId = context; return true;
        }
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        if (!await network.WaitAsync(0, ct).ConfigureAwait(false)) return;
        try
        {
            AppEvent[] batch; Guid? marker;
            lock (gate)
            {
                if (!IdentityMatches() || context is null) return;
                marker = authSession;
                batch = queue.Events.Where(e => e.CollectionContextId == context && !Expired(e)).Take(batchSize).ToArray();
                while (batch.Length > 1 && JsonSerializer.SerializeToUtf8Bytes(new { events = batch }, Json).Length > 1024 * 1024) batch = batch.Take(batch.Length / 2).ToArray();
            }
            if (batch.Length == 0) return;
            try
            {
                var body = JsonSerializer.SerializeToElement(new { events = batch }, Json);
                var result = await api.SendAuthenticatedAsync(HttpMethod.Post, "/api/events", body, ct, expectedSession: marker).ConfigureAwait(false);
                var replies = result.GetProperty("results").EnumerateArray().ToArray();
                // A malformed acknowledgement cannot remove queued events.
                if (replies.Length != batch.Length) return;
                var remove = new HashSet<Guid>(); var revoked = new HashSet<Guid>();
                for (var i = 0; i < replies.Length; i++)
                {
                    var reply = replies[i];
                    if (!reply.TryGetProperty("index", out var index) || !index.TryGetInt32(out var position) || position != i) return;
                    var status = reply.GetProperty("status").GetString();
                    if (status is not ("accepted" or "duplicate" or "rejected")) return;
                    var id = reply.GetProperty("event_id");
                    if (id.ValueKind != JsonValueKind.Null && (!id.TryGetGuid(out var parsed) || parsed != batch[i].EventId)
                        || id.ValueKind == JsonValueKind.Null && status != "rejected") return;
                    remove.Add(batch[i].EventId);
                    if (status == "rejected" && reply.TryGetProperty("code", out var code) && code.GetString() == "CONTEXT_REVOKED") revoked.Add(batch[i].CollectionContextId);
                }
                lock (gate)
                {
                    if (api.SessionMarker != marker || authSession != marker) return;
                    if (context.HasValue && revoked.Contains(context.Value)) { context = null; enabled.Clear(); }
                    Replace(queue with { Events = queue.Events.Where(e => !remove.Contains(e.EventId) && !revoked.Contains(e.CollectionContextId) && !Expired(e)).ToArray() });
                }
            }
            catch (SemojumApiException ex) when (ex.Status == HttpStatusCode.RequestEntityTooLarge)
            { batchSize = Math.Max(1, batch.Length / 2); }
            catch (SemojumApiException ex) when (ex.Status == HttpStatusCode.BadRequest)
            {
                // Whole-request validation is permanent. Splitting isolates a bad record before removal.
                if (batch.Length > 1) batchSize = Math.Max(1, batch.Length / 2);
                else lock (gate) Replace(queue with { Events = queue.Events.Where(e => e.EventId != batch[0].EventId).ToArray() });
            }
        }
        finally { network.Release(); }
    }
    private bool Expired(AppEvent e) => !DateTimeOffset.TryParse(e.OccurredAt, out var at) || time.GetUtcNow() - at > TimeSpan.FromDays(30);
    private bool Replace(EventQueue updated)
    {
        try { store.Save(updated); queue = updated; return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException) { return false; }
    }
}
