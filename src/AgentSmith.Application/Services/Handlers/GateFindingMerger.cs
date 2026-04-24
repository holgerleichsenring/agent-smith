using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Merges gate-confirmed findings with pre-existing findings by category.
/// A single "*" entry in InputCategories is the explicit wildcard — the gate
/// replaces every existing finding. Explicit categories merge: findings in
/// claimed categories are replaced, others pass through untouched.
/// </summary>
internal static class GateFindingMerger
{
    internal const string Wildcard = "*";

    internal static List<Finding> Merge(
        List<Finding> gateFindings,
        SkillOrchestration orchestration,
        PipelineContext pipeline)
    {
        if (IsWildcard(orchestration.InputCategories))
            return gateFindings;

        if (!pipeline.TryGet<IReadOnlyList<Finding>>(ContextKeys.ExtractedFindings, out var existing)
            || existing is null)
            return gateFindings;

        var claimed = new HashSet<string>(
            orchestration.InputCategories, StringComparer.OrdinalIgnoreCase);

        var merged = existing
            .Where(f => !claimed.Contains(f.Category))
            .ToList();

        merged.AddRange(gateFindings);
        return merged;
    }

    private static bool IsWildcard(IReadOnlyList<string> categories) =>
        categories.Count == 1 && categories[0] == Wildcard;
}
