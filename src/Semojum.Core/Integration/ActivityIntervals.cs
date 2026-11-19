namespace Semojum.Core.Integration;

/// <summary>Monotonic, nonoverlapping intervals. Suspended clocks produce unknown gaps, never inferred activity.</summary>
public sealed class ActivityIntervals(DateTimeOffset now, long mono, bool focused)
{
    private DateTimeOffset start = now;
    private long startMono = mono, lastInput = mono;
    private bool front = focused, ended;
    public long Sequence { get; private set; }
    public void Update(DateTimeOffset now, long mono, bool focused, bool input, bool end, Action<object> emit)
    {
        if (ended) return;
        var elapsed = mono - startMono;
        if (elapsed < 0) return;
        var gap = elapsed > 60000 || Math.Abs((now - start).TotalMilliseconds - elapsed) > 5000;
        string State(long at) => !front ? "background" : at - lastInput >= 300000 ? "idle" : "active";
        void Close(DateTimeOffset finish, long finishMono, string state, string reason)
        {
            if (finishMono == startMono) return;
            emit(new { activity_seq = ++Sequence, state, reason, interval_start = AppEvents.Stamp(start), interval_end = AppEvents.Stamp(finish),
                mono_start_ms = startMono, mono_end_ms = finishMono, duration_ms = finishMono - startMono });
            start = finish; startMono = finishMono;
        }
        if (gap) Close(now < start ? start.AddMilliseconds(elapsed) : now, mono, "unknown", "resume_gap");
        else
        {
            var idleAt = lastInput + 300000;
            if (front && startMono < idleAt && mono >= idleAt)
                Close(start.AddMilliseconds(idleAt - startMono), idleAt, "active", "state_change");
            if (end || focused != front || input && State(startMono) == "idle" || mono - startMono >= 30000)
                Close(now, mono, State(startMono), end ? "session_end" : focused != front || input ? "state_change" : "heartbeat");
        }
        if (input) lastInput = mono;
        front = focused; ended = end;
    }
}
