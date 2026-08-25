namespace AgentSmith.Infrastructure.Services.Events;

/// <summary>
/// Redis key conventions for the event backbone (p0169e). Per-run stream
/// plus two pointer indices: an active SET (members removed on RunFinished)
/// and a recent LIST (LPUSH + LTRIM on RunFinished). The pointers exist so
/// JobsBroadcaster cold-starts without scanning the keyspace.
/// </summary>
public static class EventStreamKeys
{
    public const string ActiveRunsSet = "agentsmith:runs:active";
    public const string RecentRunsList = "agentsmith:runs:recent";
    public const int RecentRunsCap = 50;
    public const int StreamMaxLen = 10_000;
    /// <summary>
    /// p0169j-a: operator look-back window. Paired with the
    /// IRunArtifactStore result-slot TTL — both default to 24h so the
    /// Trail + Result tabs share the same "yesterday's run still works"
    /// horizon. Beyond 24h, the PR is the durable surface (result.md
    /// shipped via <c>git add -A</c> in <c>CommitAndPR</c>).
    /// </summary>
    public static readonly TimeSpan StreamTtl = TimeSpan.FromHours(24);

    /// <summary>
    /// 2026-08-24-ca23: the drain's position in a run's stream, kept across a WAIT so a
    /// relaunched run continues instead of re-reading its history. Deliberately outlives
    /// <see cref="StreamTtl"/>: a position is meaningless without its stream, so "no stored
    /// position" implying "no stream to skip" is what makes starting from the beginning the
    /// correct fallback rather than a lucky one. A position that outlives its stream is
    /// harmless — stream ids are time-ordered, so a stale one precedes every new entry.
    /// </summary>
    public static readonly TimeSpan CursorTtl = TimeSpan.FromHours(48);

    public static string RunStream(string runId) => $"run:{runId}:events";

    public static string RunCursor(string runId) => $"run:{runId}:cursor";
}
