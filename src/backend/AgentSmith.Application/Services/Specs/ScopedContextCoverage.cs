using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-08-1830: decides in code whether the cut covers every context the scope call
/// named. The gap is named minus carried minus discarded — the same accounting shape
/// the ticket segments use, one level up. Run 8688's scope call named two contexts and
/// the cut carried one, and nothing mechanical compared the two.
/// <para>
/// ARMED BY THE REPLY: the check runs only when at least one phase declares contexts. A
/// catalog that never emits the field yields today's behaviour unchanged, so a binary
/// shipped ahead of the release stays safe — a version gate would have to know which pin
/// is deployed, which this reader does not.
/// </para>
/// </summary>
public sealed class ScopedContextCoverage
{
    /// <summary>The named contexts no phase carries and no discarded entry accounts for;
    /// empty when covered, when nothing was named, or when no phase declares contexts.</summary>
    public IReadOnlyList<string> Gap(ScopeNamedContexts? named, SpecSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (named is null || named.Contexts.Count == 0) return [];
        if (!set.Phases.Any(p => p.Draft.Contexts.Count > 0)) return [];
        var spokenFor = set.Phases.SelectMany(p => p.Draft.Contexts)
            .Concat(set.Accounting.DiscardedContexts.Select(d => d.Context))
            .ToList();
        return [.. named.Contexts.Where(n => !spokenFor.Any(s => ScopeNamedContexts.Matches(n, s)))];
    }

    /// <summary>The contexts the phases declare, in the order they first appear.</summary>
    public static IReadOnlyList<string> Carried(SpecSet set) =>
        [.. set.Phases.SelectMany(p => p.Draft.Contexts).Distinct(StringComparer.OrdinalIgnoreCase)];
}
