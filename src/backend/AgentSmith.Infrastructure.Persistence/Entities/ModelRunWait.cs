namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-11-20-24bd: how long a run waited for its model place — <see cref="WaitedMs"/> for the waits that ended,
/// <see cref="WaitingSince"/> while one goes on. Both wall-time clocks subtract it, so a run is not timed out for
/// waiting on another run. One row per run; deleted when the run ends or is deleted.
/// </summary>
public sealed class ModelRunWait : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public DateTimeOffset? WaitingSince { get; set; }
    public long WaitedMs { get; set; }

    /// <summary>The time waited up to <paramref name="now"/>.</summary>
    public TimeSpan Waited(DateTimeOffset now) =>
        TimeSpan.FromMilliseconds(WaitedMs) + (WaitingSince is { } since && now > since ? now - since : TimeSpan.Zero);

    /// <summary>Ends the wait going on, if any, adding it to the time waited.</summary>
    public void Close(DateTimeOffset now)
    {
        if (WaitingSince is null) return;
        WaitedMs = (long)Waited(now).TotalMilliseconds;
        WaitingSince = null;
    }
}
