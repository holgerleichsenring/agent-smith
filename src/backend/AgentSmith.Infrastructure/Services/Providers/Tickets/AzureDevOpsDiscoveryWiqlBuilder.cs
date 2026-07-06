using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0283b: WIQL builder for composed claimable discovery. Each branch becomes
/// <c>([System.State] IN (...) AND (Tags CONTAINS | AreaPath UNDER))</c>, OR'd; a branch
/// with no expressible part falls to the open set minus the parking statuses (never the
/// leaky bare open set). Tag/AreaPath are the only WIQL-expressible strategies — Repo and
/// ToAddress stay in the in-process matcher.
/// </summary>
public sealed class AzureDevOpsDiscoveryWiqlBuilder : IAzureDevOpsDiscoveryWiqlBuilder
{
    public string BuildWhere(DiscoveryQuery query, IReadOnlyList<string> openStates)
    {
        var routing = query.Branches.Count == 0
            ? BroadClause(query.ParkingStatuses, openStates)
            : string.Join(" OR ",
                query.Branches.Select(b => BranchClause(b, query.ParkingStatuses, openStates)));
        // Only tickets carrying an agent-smith trigger/lifecycle label are ever claimable —
        // CONTAINS is a substring match, so the prefix guard covers every agent-smith:* tag and
        // stops discovery from hydrating + event-spamming every project-tagged ticket each poll.
        return $"({routing}) AND [System.Tags] CONTAINS '{LifecycleLabels.Prefix}'";
    }

    private static string BranchClause(
        DiscoveryBranch branch, IReadOnlyList<string> parking, IReadOnlyList<string> openStates)
    {
        var parts = new List<string>(2);
        if (branch.Statuses.Count > 0) parts.Add(StateIn(branch.Statuses));
        if (CriterionClause(branch.Criterion) is { } criterion) parts.Add(criterion);
        return parts.Count == 0
            ? BroadClause(parking, openStates)
            : $"({string.Join(" AND ", parts)})";
    }

    private static string? CriterionClause(DiscoveryCriterion? criterion)
    {
        if (criterion is null) return null;
        return criterion.Strategy switch
        {
            ResolutionStrategy.Tag      => $"[System.Tags] CONTAINS '{Escape(criterion.Value)}'",
            ResolutionStrategy.AreaPath => $"[System.AreaPath] UNDER '{Escape(criterion.Value)}'",
            _ => null,   // Repo / ToAddress — not WIQL-expressible, stay in-process
        };
    }

    private static string BroadClause(IReadOnlyList<string> parking, IReadOnlyList<string> openStates)
        => parking.Count == 0
            ? $"({StateIn(openStates)})"
            : $"({StateIn(openStates)} AND [System.State] NOT IN ({StatesList(parking)}))";

    private static string StateIn(IReadOnlyList<string> states) => $"[System.State] IN ({StatesList(states)})";

    private static string StatesList(IReadOnlyList<string> states) =>
        string.Join(", ", states.Select(s => $"'{Escape(s)}'"));

    private static string Escape(string value) => value.Replace("'", "''");
}
