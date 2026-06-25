using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Models.Triggers;

/// <summary>
/// Helpers for translating a <see cref="DiscoveryQuery"/> for the label-only providers
/// (GitHub/GitLab), whose native status is just open/closed so they narrow by the resolution
/// tag + open state rather than a per-branch status clause.
/// </summary>
public static class DiscoveryQueryExtensions
{
    /// <summary>
    /// The distinct Tag-resolution labels across ALL branches, or null when the query cannot be
    /// expressed purely as a label filter — no branches, a broad branch, or a non-Tag criterion
    /// (AreaPath/Repo/ToAddress). Null means the provider must stay broad (ListOpenAsync).
    /// </summary>
    public static IReadOnlyList<string>? AllTagLabelsOrNull(this DiscoveryQuery query)
    {
        if (query.Branches.Count == 0) return null;
        var labels = new List<string>(query.Branches.Count);
        foreach (var branch in query.Branches)
        {
            if (branch.Criterion is not { Strategy: ResolutionStrategy.Tag } tag) return null;
            labels.Add(tag.Value);
        }
        return labels.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
