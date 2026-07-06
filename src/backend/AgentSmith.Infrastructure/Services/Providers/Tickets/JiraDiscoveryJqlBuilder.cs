using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0283b: JQL builder for composed claimable discovery. Each branch becomes
/// <c>(status IN (...) AND labels = "tag")</c>, OR'd — status AND the resolution tag pushed
/// server-side so a tag-less ticket in a trigger status is never fetched (the 1000-To-Do case).
/// A no-criterion or no-branch query falls to <c>statusCategory != Done AND status NOT IN
/// (parking)</c> — a positive/exclusion shape that, unlike the bare <c>!= Done</c>, never
/// re-reads agent-smith's own done/failed status. JQL <c>labels =</c> is EXACT (case-sensitive):
/// the operator sets both the config tag and the ticket label, so they align; the in-process
/// matcher stays the backstop. AreaPath/Repo/ToAddress aren't JQL-expressible → status only.
/// </summary>
public sealed class JiraDiscoveryJqlBuilder : IJiraDiscoveryJqlBuilder
{
    public string BuildJql(DiscoveryQuery query)
    {
        var routing = query.Branches.Count == 0
            ? BroadClause(query.ParkingStatuses)
            : string.Join(" OR ", query.Branches
                .Select(b => BranchClause(b, query.ParkingStatuses))
                .Distinct());
        // A ticket is only claimable when it carries an agent-smith trigger label. JQL labels=
        // is exact (no prefix), so enumerate the tracker's configured keys; empty = no guard.
        return query.TriggerLabels.Count == 0
            ? routing
            : $"({routing}) AND labels IN ({LabelList(query.TriggerLabels)})";
    }

    private static string LabelList(IReadOnlyList<string> labels) =>
        string.Join(", ", labels.Select(l => $"\"{Escape(l)}\""));

    private static string BranchClause(DiscoveryBranch branch, IReadOnlyList<string> parking)
    {
        var parts = new List<string>(2);
        if (branch.Statuses.Count > 0) parts.Add($"status IN ({StatusList(branch.Statuses)})");
        if (LabelClause(branch.Criterion) is { } label) parts.Add(label);
        return parts.Count == 0 ? BroadClause(parking) : $"({string.Join(" AND ", parts)})";
    }

    private static string? LabelClause(DiscoveryCriterion? criterion)
    {
        if (criterion is null || criterion.Strategy != ResolutionStrategy.Tag) return null;
        return $"labels = \"{Escape(criterion.Value)}\"";
    }

    private static string BroadClause(IReadOnlyList<string> parking)
        => parking.Count == 0
            ? "(statusCategory != Done)"
            : $"(statusCategory != Done AND status NOT IN ({StatusList(parking)}))";

    private static string StatusList(IReadOnlyList<string> statuses) =>
        string.Join(", ", statuses.Select(s => $"\"{Escape(s)}\""));

    private static string Escape(string value) => value.Replace("\"", "\\\"");
}
