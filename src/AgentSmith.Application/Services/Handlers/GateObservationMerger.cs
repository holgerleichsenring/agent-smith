using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Merges gate-emitted SkillObservations with the existing observation list by
/// category. Wildcard "*" replaces all observations; explicit categories merge:
/// observations in claimed categories are dropped, others pass through, and the
/// gate's emitted observations are appended.
/// </summary>
internal static class GateObservationMerger
{
    internal const string Wildcard = "*";

    internal static List<SkillObservation> Merge(
        List<SkillObservation> gateObservations,
        SkillOrchestration orchestration,
        PipelineContext pipeline)
    {
        if (IsWildcard(orchestration.InputCategories))
            return gateObservations;

        if (!pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var existing)
            || existing is null)
            return gateObservations;

        var claimed = new HashSet<string>(
            orchestration.InputCategories, StringComparer.OrdinalIgnoreCase);

        var merged = existing
            .Where(o => o.Category is null || !claimed.Contains(o.Category))
            .ToList();

        merged.AddRange(gateObservations);
        return merged;
    }

    private static bool IsWildcard(IReadOnlyList<string> categories) =>
        categories.Count == 1 && categories[0] == Wildcard;
}
