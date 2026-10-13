using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-13-9a59e: the cut a published set carries — every spec in set order with what it was asked
/// to do. The ordinal is the spec's 1-based place in the set, the same number its state events carry.
/// </summary>
public static class SpecCutContent
{
    public static IReadOnlyList<SpecCutEntry> Of(SpecSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        return [.. set.Specs.Select((spec, i) => new SpecCutEntry(
            spec.SpecId, i + 1, spec.FileStem, spec.Draft.Goal, spec.Draft.Repos,
            spec.Draft.Steps, spec.Draft.Done, spec.Draft.Yaml))];
    }

    /// <summary>The case of a handed-back set, as the cut stores it; null for a set that was not.</summary>
    public static string? CaseOf(SpecHandback? handback) =>
        handback is { Case: not SpecHandbackCase.None } ? handback.Case.ToString() : null;
}
