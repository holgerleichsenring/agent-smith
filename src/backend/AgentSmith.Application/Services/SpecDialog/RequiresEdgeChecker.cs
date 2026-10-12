using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// p0315e: consistency check over an epic's requires: edges. Spec-id-shaped
/// entries in a child's requires must name an existing SIBLING (free-text
/// preconditions pass through — the schema allows them), ids must be unique,
/// and the sibling edges must not form a cycle.
/// </summary>
public sealed partial class RequiresEdgeChecker
{
    // p0507: both namespaces. An id shape this does NOT match is read as a free-text
    // precondition, so a missed shape silently passes sibling, parent and cycle checks.
    // 2026-10-02-3f06c: public, so the proposal evidence check reads ids by this one regex.
    [GeneratedRegex(@"^(?:p\d+[a-z]?|\d{4}-\d{2}-\d{2}-[0-9a-f]{4}[a-z]?)(-[a-z][a-z0-9-]*)?$")]
    public static partial Regex SpecIdRegex();

    public string? Check(SpecDraft parent, IReadOnlyList<SpecDraft> children) =>
        CheckIdsUnique(parent, children)
        ?? CheckEdgesPointAtSiblings(parent, children)
        ?? CheckNoCycles(children);

    private static string? CheckIdsUnique(SpecDraft parent, IReadOnlyList<SpecDraft> children)
    {
        var duplicate = children.Select(c => c.SpecId)
            .Append(parent.SpecId)
            .GroupBy(id => id, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1);
        return duplicate is null ? null : $"epic spec id '{duplicate.Key}' is used more than once";
    }

    private static string? CheckEdgesPointAtSiblings(
        SpecDraft parent, IReadOnlyList<SpecDraft> children)
    {
        var siblingIds = children.Select(c => c.SpecId).ToHashSet(StringComparer.Ordinal);
        foreach (var child in children)
        {
            foreach (var requirement in child.Requires.Where(r => SpecIdRegex().IsMatch(r)))
            {
                if (requirement == parent.SpecId)
                    return $"child '{child.SpecId}' requires the parent '{parent.SpecId}' — "
                        + "the parent aggregates the children, it is not a dependency";
                if (!siblingIds.Contains(requirement))
                    return $"child '{child.SpecId}' requires '{requirement}', which is not a "
                        + "sibling in this epic — express external preconditions as free text";
            }
        }
        return null;
    }

    private static string? CheckNoCycles(IReadOnlyList<SpecDraft> children)
    {
        var edges = children.ToDictionary(
            c => c.SpecId,
            c => c.Requires.Where(r => SpecIdRegex().IsMatch(r)).ToList(),
            StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var child in children)
        {
            if (HasCycle(child.SpecId, edges, visited, new HashSet<string>(StringComparer.Ordinal)))
                return $"the requires: edges form a cycle through '{child.SpecId}'";
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
