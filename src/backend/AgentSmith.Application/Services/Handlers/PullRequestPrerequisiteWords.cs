using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-25-96aeb: the refusal of a merge whose prerequisites have not landed, naming per prerequisite where it has to land —
/// the series branch for a ticket of the series that integrates there, the default branch for any other.
/// </summary>
public static class PullRequestPrerequisiteWords
{
    public static string Refusal(IReadOnlyList<TicketRequirementEdge> unmet, string? seriesBranch, IReadOnlyCollection<string> members)
    {
        ArgumentNullException.ThrowIfNull(unmet);
        ArgumentNullException.ThrowIfNull(members);
        var groups = unmet.DistinctBy(e => e.RequiredTicketId)
            .GroupBy(e => seriesBranch is not null && members.Contains(e.RequiredTicketId) ? $"the series branch '{seriesBranch}'" : "the default branch")
            .Select(g => Line([.. g.Select(e => RequiresUnmetGate.Named(e.RequiredTicketId))], g.Key));
        return $"not merged: {string.Join("; ", groups)}";
    }

    private static string Line(IReadOnlyList<string> named, string target) => named.Count == 1
        ? $"{named[0]}, a prerequisite, is not merged into {target} yet"
        : $"prerequisites {RequiresUnmetGate.MergesNamed(named)} are not merged into {target} yet";
}
