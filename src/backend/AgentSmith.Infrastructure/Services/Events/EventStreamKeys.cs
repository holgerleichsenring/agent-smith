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
    public static readonly TimeSpan StreamTtl = TimeSpan.FromHours(2);

    public static string RunStream(string runId) => $"run:{runId}:events";
}
