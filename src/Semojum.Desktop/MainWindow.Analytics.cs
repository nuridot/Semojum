using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Semojum.Core;
using Semojum.Core.Integration;
using Semojum.Desktop.Dialogs;
using Semojum.Desktop.Services;

namespace Semojum.Desktop;

public partial class MainWindow
{
    private AppEvents? appEvents;
    private WindowsAppEventStore? eventStore;
    private DispatcherTimer? eventTimer;
    private readonly Dictionary<DocumentSession, EventDocument> eventDocuments = [];
    private readonly Stopwatch eventClock = Stopwatch.StartNew();
    private bool eventPumpRunning;
    private Task? eventConfigTask;
    private Task? eventPumpTask;
    private DateTimeOffset lastEventFlush, lastEventConfig;
    private TimeSpan eventFlushDelay = TimeSpan.FromSeconds(30);
    private sealed class EventDocument(AppEventSession session, ActivityIntervals activity, long version)
    {
        public AppEventSession Session = session;
        public ActivityIntervals Activity = activity;
        public long Version = version;
        public Guid Revision = Guid.NewGuid();
        public Dictionary<string, Guid> FailedOperations = [];
        public Dictionary<string, string> Summaries = [];
        public bool Completed;
        public bool Opened;
    }
    private void InitializeAppEvents()
    {
        if (smoke) return;
        try
        {
            eventStore = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Semojum", "app-events"));
            appEvents = new(api, eventStore, CurrentVersion.ToString(3));
            eventTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => EventTick(), Dispatcher);
            InputManager.Current.PreProcessInput += EventInput;
            NetworkChange.NetworkAvailabilityChanged += EventNetworkChanged;
            _ = RefreshEventConfigAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        { eventStore?.Dispose(); eventStore = null; appEvents = null; }
    }
    private bool EventFocused(DocumentSession model) => ReferenceEquals(session, model) && WindowState != WindowState.Minimized
        && Application.Current.Windows.Cast<Window>().Any(w => w.IsActive && IsEditorWindow(w));
    private bool IsEditorWindow(Window window)
    {
        for (Window? at = window; at is not null; at = at.Owner) if (ReferenceEquals(at, this)) return true;
        return false;
    }
    private void EventInput(object sender, PreProcessInputEventArgs e)
    {
        if (e.StagingItem.Input is not (KeyEventArgs or MouseButtonEventArgs or MouseWheelEventArgs or TextCompositionEventArgs)) return;
        if (session is null || !EventFocused(session)) return;
        var tracked = TrackEvents(session);
        if (tracked is not null) UpdateActivity(session, tracked, input: true);
    }
    private void EventNetworkChanged(object? sender, NetworkAvailabilityEventArgs e)
    { if (e.IsAvailable && !resourcesReleased) _ = Dispatcher.BeginInvoke(new Action(() => _ = RefreshEventConfigAsync())); }
    private Task RefreshEventConfigAsync() => eventConfigTask is { IsCompleted: false } running ? running : eventConfigTask = RefreshEventConfigCoreAsync();
    private async Task RefreshEventConfigCoreAsync()
    {
        if (appEvents is null || resourcesReleased) return;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(appLifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            await appEvents.RefreshAsync(timeout.Token);
            lastEventConfig = DateTimeOffset.UtcNow;
        }
        catch (Exception ex) when (ex is HttpRequestException or SemojumApiException or OperationCanceledException or JsonException or InvalidDataException or KeyNotFoundException) { }
    }
    private void EventTick()
    {
        if (resourcesReleased || appEvents is null) return;
        foreach (var (model, tracked) in eventDocuments.ToArray())
        {
            if (tracked.Session.ContextId != appEvents.ContextId) { eventDocuments.Remove(model); continue; }
            if (tracked.Completed && tracked.Version != model.ChangeVersion) { TrackEvents(model); continue; }
            if (!tracked.Completed) UpdateActivity(model, tracked);
        }
        if (!eventPumpRunning && DateTimeOffset.UtcNow - lastEventFlush > eventFlushDelay) eventPumpTask = PumpEventsAsync();
    }
    private async Task PumpEventsAsync()
    {
        if (appEvents is null) return;
        eventPumpRunning = true; lastEventFlush = DateTimeOffset.UtcNow;
        try
        {
            // Refresh before every upload, including the first attempt after an offline period.
            if (appEvents.ContextId is not null || DateTimeOffset.UtcNow - lastEventConfig > TimeSpan.FromMinutes(5)) await RefreshEventConfigAsync();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(appLifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            await appEvents.FlushAsync(timeout.Token);
            eventFlushDelay = TimeSpan.FromSeconds(30);
        }
        catch (Exception ex) when (ex is HttpRequestException or SemojumApiException or OperationCanceledException or JsonException or InvalidDataException or KeyNotFoundException)
        { eventFlushDelay = TimeSpan.FromSeconds(Math.Min(300, eventFlushDelay.TotalSeconds * 2 + Random.Shared.Next(0, 10))); }
        finally { eventPumpRunning = false; }
    }
    private EventDocument? TrackEvents(DocumentSession model, bool announce = true)
    {
        if (appEvents?.ContextId is not { } context
            || model.OwnerLoginId is { } owner && owner != api.Account?.LoginId) return null;
        if (openDocuments.Any(d => ReferenceEquals(d.Model, model) && IsUntouchedPlaceholder(d))) return null;
        if (eventDocuments.TryGetValue(model, out var found))
        {
            if (found.Session.ContextId == context && (!found.Completed || found.Version == model.ChangeVersion)) return found;
            eventDocuments.Remove(model);
        }
        var identity = model.AppIdentity is { } old ? old with { WorkRevision = old.WorkRevision + 1 } : new AppDocumentIdentity(Guid.NewGuid(), 1);
        model.AppIdentity = identity;
        var work = new AppEventSession(identity.Id, identity.WorkRevision);
        appEvents.AnalyticsContext(work);
        var tracked = new EventDocument(work, new(DateTimeOffset.UtcNow, eventClock.ElapsedMilliseconds, EventFocused(model)), model.ChangeVersion);
        eventDocuments.Add(model, tracked);
        if (announce)
        {
            using var opened = EventOperation(model, "document_open_result", new(), tracked: tracked);
            opened.Success();
        }
        return tracked;
    }
    private Guid EventRevision(DocumentSession model, EventDocument tracked)
    {
        if (tracked.Version != model.ChangeVersion) { tracked.Version = model.ChangeVersion; tracked.Revision = Guid.NewGuid(); }
        return tracked.Revision;
    }
    private AppEventOperation EventOperation(DocumentSession model, string name, Dictionary<string, object?> extra, EventDocument? tracked = null)
    {
        tracked ??= TrackEvents(model, announce: name != "document_open_result");
        var retryKey = name + (extra.GetValueOrDefault("format") as string ?? "");
        var retry = tracked?.FailedOperations.GetValueOrDefault(retryKey);
        return new(properties =>
        {
            if (tracked is null || tracked.Completed) return;
            var data = (IDictionary<string, object?>)properties;
            if ((string)data["result"]! == "failure") tracked.FailedOperations[retryKey] = (Guid)data["operation_id"]!;
            else if ((string)data["result"]! == "success") tracked.FailedOperations.Remove(retryKey);
            if (name == "document_open_result" && (string)data["result"]! == "success") tracked.Opened = true;
            appEvents?.Record(tracked.Session, name, model.JobId, properties);
        }, extra, retry is { } id && id != Guid.Empty ? id : null);
    }
    private AppEventOperation EventOpen(Func<DocumentSession> model) => new(properties =>
    {
        var owner = model(); var tracked = TrackEvents(owner, announce: false);
        if (tracked is null) return;
        var succeeded = ((IDictionary<string, object?>)properties)["result"] as string == "success";
        if (tracked.Opened && succeeded) return;
        tracked.Opened = succeeded;
        appEvents?.Record(tracked.Session, "document_open_result", owner.JobId, properties);
        if (!succeeded) CloseEventDocument(owner);
    }, new Dictionary<string, object?>());
    private object? EventContext(DocumentSession model)
    {
        var tracked = TrackEvents(model);
        return tracked is null ? null : appEvents?.AnalyticsContext(tracked.Session);
    }
    private void UpdateActivity(DocumentSession model, EventDocument tracked, bool input = false, bool end = false)
    {
        if (appEvents?.Allows("activity_state_changed") != true || tracked.Completed) return;
        tracked.Activity.Update(DateTimeOffset.UtcNow, eventClock.ElapsedMilliseconds, EventFocused(model), input, end,
            data => appEvents.Record(tracked.Session, "activity_state_changed", model.JobId, data));
    }
    private void CloseEventDocument(DocumentSession model)
    { if (eventDocuments.Remove(model, out var tracked)) UpdateActivity(model, tracked, end: true); }
    private void TransferEvents(DocumentSession from, DocumentSession to)
    {
        if (!eventDocuments.Remove(from, out var tracked)) return;
        to.AppIdentity = from.AppIdentity; tracked.Version = to.ChangeVersion; eventDocuments[to] = tracked;
    }
    private async Task EmitRevisionSummariesAsync(DocumentSession model, SmjManifest snapshot, Guid savedRevision, bool completion)
    {
        if (appEvents?.Allows("ai_revision_summary") != true || snapshot.JobId is null || !eventDocuments.TryGetValue(model, out var tracked)) return;
        try
        {
        foreach (var block in snapshot.Blocks.Where(b => b.ServerId is not null && b.AiBaseline is not null))
        {
            foreach (var representation in new[] { "text", "braille" })
            {
                var baseline = representation == "text" ? block.AiBaseline!.Text : block.EngineDraft is { CapturePending: false } d ? d.Contents : null;
                var final = representation == "text" ? block.Source : block.Braille;
                var key = $"{block.ServerId}:{block.AiBaseline!.VersionId}:{representation}";
                if (!completion && tracked.Summaries.TryGetValue(key, out var previous) && previous == final) continue;
                var comparison = await Task.Run(() => RevisionComparison.Compare(baseline, final));
                var properties = JsonSerializer.SerializeToElement(comparison, AppEvents.Json).EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.Clone());
                properties["target_id"] = block.ServerId; properties["baseline_version_id"] = block.AiBaseline.VersionId;
                properties["saved_revision_id"] = savedRevision; properties["representation"] = representation;
                if (appEvents.Record(tracked.Session, "ai_revision_summary", snapshot.JobId, properties)) tracked.Summaries[key] = final;
            }
        }
        }
        catch (Exception ex) when (ex is JsonException or OperationCanceledException or InvalidOperationException)
        { /* Analytics failure must never turn a confirmed document save into a save error. */ }
    }
    private async Task CompleteWorkAsync()
    {
        if (fileBusy || session.IsReadOnly) return;
        var owner = session; var tracked = TrackEvents(owner);
        if (tracked?.Completed == true) { owner.Status = "이미 완료한 작업입니다. 다시 편집하면 새 작업 회차가 시작됩니다."; return; }
        var windows = Application.Current.Windows.Cast<Window>().Where(IsEditorWindow).Select(w => (Window: w, Enabled: w.IsEnabled)).ToArray();
        foreach (var w in windows) w.Window.IsEnabled = false;
        try
        {
            if (!await SaveTrackedAsync(false, completion: true)) return;
            // Background translation can still change the model while the save is running.
            if (owner.IsDirty) return;
            if (tracked is not null)
            {
                UpdateActivity(owner, tracked, end: true);
                var properties = new Dictionary<string, object?> { ["completion_action"] = "explicit", ["saved_revision_id"] = tracked.Revision };
                if (appEvents?.Allows("activity_state_changed") == true && tracked.Activity.Sequence > 0) properties["activity_last_seq"] = tracked.Activity.Sequence;
                appEvents?.Record(tracked.Session, "job_completed", owner.JobId, properties);
                tracked.Completed = true; tracked.Session.Completed = true;
            }
            owner.Status = "작업을 완료했습니다. 다시 편집하면 새 작업 회차가 시작됩니다.";
        }
        finally { foreach (var w in windows) w.Window.IsEnabled = w.Enabled; }
    }
    private void StopAppEvents()
    {
        eventTimer?.Stop();
        if (!smoke) { InputManager.Current.PreProcessInput -= EventInput; NetworkChange.NetworkAvailabilityChanged -= EventNetworkChanged; }
        foreach (var model in eventDocuments.Keys.ToArray()) CloseEventDocument(model);
        // Durable records will be retried on next login; shutdown does not block on a network call.
    }
    private async Task FinishAppEventsAsync()
    {
        try { await Task.WhenAll(eventConfigTask ?? Task.CompletedTask, eventPumpTask ?? Task.CompletedTask); }
        finally { eventStore?.Dispose(); }
    }
}
