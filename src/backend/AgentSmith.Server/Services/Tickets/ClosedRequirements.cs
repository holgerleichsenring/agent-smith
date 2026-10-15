using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-15-114fb: flags each never-run row's waits-for entry whose required ticket is in the closed bucket. A
/// requirement is keyed by the dependent's tracker connection and the required key, so the entry is matched by
/// (its project's tracker connection, the key rebuilt from that tracker's type) — never by (project, id), which
/// misses a required ticket of another project on the same connection.
/// </summary>
internal static class ClosedRequirements
{
    public static TicketListAll Apply(TicketListAll all, Func<string, ResolvedProject?> projectOf)
    {
        var summaries = TicketNodeStates.Summaries(all, projectOf);
        bool IsClosed(TicketWaitsForView entry) =>
            entry.Project is { } project && projectOf(project) is { } resolved
            && summaries.GetValueOrDefault((resolved.Tracker.Name,
                TicketKey.For(resolved.Tracker.Type.ToString().ToLowerInvariant(), entry.TicketId).Value))
                ?.Bucket == TicketBucket.Closed;
        return all with
        {
            NeverRun = [.. all.NeverRun.Select(s => s.WaitsFor is { Count: > 0 } waits
                ? s with { WaitsFor = [.. waits.Select(w => IsClosed(w) ? w with { Closed = true } : w)] }
                : s)],
        };
    }
}
