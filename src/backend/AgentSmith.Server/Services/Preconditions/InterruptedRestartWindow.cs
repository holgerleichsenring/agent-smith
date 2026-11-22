namespace AgentSmith.Server.Services.Preconditions;

/// <summary>
/// 2026-11-22-6d85c: the interruptions agent-smith restarts by itself — those that ended since shortly before this process
/// started, so a deploy does not retry every interruption from before the restart existed. One per process; the restart
/// pass and the series row read the same window, so a row says 'restarting' only for an attempt the pass will retry.
/// </summary>
public sealed class InterruptedRestartWindow(TimeProvider clock)
{
    /// <summary>How long before this process started an interruption may lie — the gap between a stopping server's last
    /// end and the next one's first pass.</summary>
    public static readonly TimeSpan Horizon = TimeSpan.FromHours(1); // not a limit: the restart window of one deploy

    /// <summary>The earliest end the restart takes.</summary>
    public DateTimeOffset Since { get; } = clock.GetUtcNow() - Horizon;

    /// <summary>Whether an interruption that ended at <paramref name="finishedAt"/> is restarted.</summary>
    public bool Covers(DateTimeOffset? finishedAt) => finishedAt is { } at && at >= Since;
}
