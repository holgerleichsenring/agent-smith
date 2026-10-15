using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-14-dbcen: a graph node's and a requires entry's state, from the list's own summaries — a landing
/// outranks everything because it is permanent, then running, blocked and ready, then the list bucket.
/// </summary>
internal static class TicketNodeStates
{
    public const string Merged = "merged";
    public const string DoneByHand = "done-by-hand";
    public const string Running = "running";
    public const string Ready = "ready";

    public static string Of(TicketLandingFact? landing, TicketSummaryView? summary) =>
        landing is not null ? (landing.Kind == TicketLandingKinds.ByHand ? DoneByHand : Merged)
        : summary is null ? TicketBucket.Idle
        : summary.Bucket switch
        {
            TicketBucket.Working => Running,
            TicketBucket.Filed => Ready,
            var bucket => bucket,
        };

    /// <summary>Every summary by (tracker connection, ticket key); a run-bearing one wins over a never-run one.</summary>
    public static Dictionary<(string Tracker, string Key), TicketSummaryView> Summaries(
        TicketListAll all, Func<string, ResolvedProject?> projectOf)
    {
        var byKey = new Dictionary<(string, string), TicketSummaryView>();
        foreach (var summary in all.Runs.Select(r => r.Summary).Concat(all.NeverRun))
            if (summary.TicketKey is { } key && projectOf(summary.Project)?.Tracker.Name is { } tracker)
                byKey.TryAdd((tracker, key), summary);
        return byKey;
    }

    /// <summary>The landings of the named keys, grouped per tracker connection.</summary>
    public static async Task<Dictionary<(string Tracker, string Key), TicketLandingFact>> LandedAsync(
        AgentSmith.Contracts.Services.ITicketLandingStore landings,
        IEnumerable<(string Tracker, string Key)> keys, CancellationToken ct)
    {
        var landed = new Dictionary<(string, string), TicketLandingFact>();
        foreach (var tracker in keys.Distinct().GroupBy(k => k.Tracker))
            foreach (var landing in await landings.LandedAsync(tracker.Key, [.. tracker.Select(k => k.Key)], ct))
                landed[(landing.Tracker, landing.TicketKey)] = landing;
        return landed;
    }

    /// <summary>2026-10-15-114fb: the close marks of the named keys, grouped per tracker connection.</summary>
    public static async Task<Dictionary<(string Tracker, string Key), TicketClosedMark>> ClosedAsync(
        AgentSmith.Contracts.Services.ITicketClosedMarkStore marks,
        IEnumerable<(string Tracker, string Key)> keys, CancellationToken ct)
    {
        var closed = new Dictionary<(string, string), TicketClosedMark>();
        foreach (var tracker in keys.Distinct().GroupBy(k => k.Tracker))
            foreach (var mark in await marks.OfAsync(tracker.Key, [.. tracker.Select(k => k.Key)], ct))
                closed[(mark.Tracker, mark.TicketKey)] = mark;
        return closed;
    }
}
