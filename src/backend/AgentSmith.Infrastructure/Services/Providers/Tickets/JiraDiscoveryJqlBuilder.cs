using AgentSmith.Contracts.Models.Triggers;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0283b: JQL builder for composed claimable discovery. Each status-constrained branch
/// becomes <c>(status IN (...))</c>, OR'd; a status-unconstrained or no-branch query falls
/// to <c>statusCategory != Done AND status NOT IN (parking)</c> — a positive/exclusion shape
/// that, unlike the bare <c>!= Done</c>, never re-reads agent-smith's own done/failed status.
/// The tag is NOT pushed (JQL <c>labels =</c> is case-sensitive); the in-process matcher keeps it.
/// </summary>
public sealed class JiraDiscoveryJqlBuilder : IJiraDiscoveryJqlBuilder
{
    public string BuildJql(DiscoveryQuery query)
    {
        if (query.Branches.Count == 0) return BroadClause(query.ParkingStatuses);
        var clauses = query.Branches
            .Select(b => BranchClause(b, query.ParkingStatuses))
            .Distinct()
            .ToList();
        return string.Join(" OR ", clauses);
    }

    private static string BranchClause(DiscoveryBranch branch, IReadOnlyList<string> parking)
        => branch.Statuses.Count > 0
            ? $"(status IN ({StatusList(branch.Statuses)}))"
            : BroadClause(parking);

    private static string BroadClause(IReadOnlyList<string> parking)
        => parking.Count == 0
            ? "(statusCategory != Done)"
            : $"(statusCategory != Done AND status NOT IN ({StatusList(parking)}))";

    private static string StatusList(IReadOnlyList<string> statuses) =>
        string.Join(", ", statuses.Select(s => $"\"{Escape(s)}\""));

    private static string Escape(string value) => value.Replace("\"", "\\\"");
}
