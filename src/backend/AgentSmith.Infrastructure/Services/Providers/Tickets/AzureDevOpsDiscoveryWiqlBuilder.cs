using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;

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
        // p0300c-hotfix: guard by the tracker's configured trigger labels (the
        // pipeline_from_label keys, carried on DiscoveryQuery.TriggerLabels) ONLY when the
        // tracker uses label opt-in — mirrors the Jira builder. A tracker that triggers on
        // area_path/status has NO trigger labels → NO guard; the original unconditional
        // `CONTAINS 'agent-smith:'` prefix excluded every fresh, un-lifecycle-tagged work item
        // and broke AzDO reception. Full-tag CONTAINS (not a bare prefix) is AzDO's reliable
        // tag filter; a label-gated ticket that lacks a trigger label is dropped in-process
        // anyway, so this never hides a claimable ticket.
        if (query.TriggerLabels.Count == 0) return Admitting(routing, query.ApprovedTicketIds);
        var labelGuard = string.Join(" OR ",
            query.TriggerLabels.Select(l => $"[System.Tags] CONTAINS '{Escape(l)}'"));
        return Admitting($"({routing}) AND ({labelGuard})", query.ApprovedTicketIds);
    }

    /// <summary>
    /// 2026-09-25-c1f7: the tickets an approved record still expects work on, OR'd with the WHOLE
    /// query. It cannot be another entry in the label guard: the guard is AND-ed onto the routing
    /// clause, so a list that must ADMIT what the guard excludes has to sit beside it. A work item
    /// whose stamp somebody deleted is fetched because of this clause and nothing else.
    /// <para>
    /// NON-NUMERIC IDS ARE DROPPED. <c>[System.Id]</c> is an integer field and WIQL refuses a
    /// quoted value against it, so one Jira-shaped id carried on a mixed store would make the
    /// whole poll fail rather than widen it. The stored id is the tracker's own, so on an Azure
    /// DevOps connection every one of them is already a number.
    /// </para>
    /// </summary>
    private static string Admitting(string guarded, IReadOnlyList<string> approvedTicketIds)
    {
        var numeric = approvedTicketIds.Where(id => long.TryParse(id, out _)).ToList();
        return numeric.Count == 0
            ? guarded
            : $"({guarded}) OR [System.Id] IN ({string.Join(", ", numeric)})";
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
