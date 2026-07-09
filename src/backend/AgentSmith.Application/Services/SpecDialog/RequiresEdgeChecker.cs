using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// p0315e: consistency check over an epic's requires: edges. Phase-id-shaped
/// entries in a child's requires must name an existing SIBLING (free-text
/// preconditions pass through — the schema allows them), ids must be unique,
/// and the sibling edges must not form a cycle.
/// </summary>
public sealed partial class RequiresEdgeChecker
{
    [GeneratedRegex(@"^p\d+[a-z]?(-[a-z][a-z0-9-]*)?$")]
    private static partial Regex PhaseIdRegex();

    public string? Check(PhaseDraft parent, IReadOnlyList<PhaseDraft> children) =>
        CheckIdsUnique(parent, children)
        ?? CheckEdgesPointAtSiblings(parent, children)
        ?? CheckNoCycles(children);

    private static string? CheckIdsUnique(PhaseDraft parent, IReadOnlyList<PhaseDraft> children)
    {
        var duplicate = children.Select(c => c.PhaseId)
            .Append(parent.PhaseId)
            .GroupBy(id => id, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1);
        return duplicate is null ? null : $"epic phase id '{duplicate.Key}' is used more than once";
    }

    private static string? CheckEdgesPointAtSiblings(
        PhaseDraft parent, IReadOnlyList<PhaseDraft> children)
    {
        var siblingIds = children.Select(c => c.PhaseId).ToHashSet(StringComparer.Ordinal);
        foreach (var child in children)
        {
            foreach (var requirement in child.Requires.Where(r => PhaseIdRegex().IsMatch(r)))
            {
                if (requirement == parent.PhaseId)
                    return $"child '{child.PhaseId}' requires the parent '{parent.PhaseId}' — "
                        + "the parent aggregates the children, it is not a dependency";
                if (!siblingIds.Contains(requirement))
                    return $"child '{child.PhaseId}' requires '{requirement}', which is not a "
                        + "sibling in this epic — express external preconditions as free text";
            }
        }
        return null;
    }

    private static string? CheckNoCycles(IReadOnlyList<PhaseDraft> children)
    {
        var edges = children.ToDictionary(
            c => c.PhaseId,
            c => c.Requires.Where(r => PhaseIdRegex().IsMatch(r)).ToList(),
            StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var child in children)
        {
            if (HasCycle(child.PhaseId, edges, visited, new HashSet<string>(StringComparer.Ordinal)))
                return $"the requires: edges form a cycle through '{child.PhaseId}'";
        }
        return null;
    }

    private static bool HasCycle(
        string id, IReadOnlyDictionary<string, List<string>> edges,
        HashSet<string> visited, HashSet<string> path)
    {
        if (!path.Add(id)) return true;
        if (visited.Add(id) && edges.TryGetValue(id, out var requirements)
            && requirements.Any(r => HasCycle(r, edges, visited, path)))
            return true;
        path.Remove(id);
        return false;
    }
}
