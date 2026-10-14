using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762e: whether a spec's citations describe the tree as it is now. A spec whose requires name
/// a member of its own set that has not been built cites files that member creates, so its citations
/// describe the state the requires leave. A requires id outside the set — a done spec, a predecessor of
/// the series — counts as built; otherwise most members of a series would never be checked.
/// </summary>
public static class SpecToday
{
    public static bool Describes(SpecDraft draft, SpecSet set)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(set);
        return draft.Requires.All(id =>
            set.Executed.Contains(id, StringComparer.Ordinal)
            || !set.Specs.Any(spec => string.Equals(spec.SpecId, id, StringComparison.Ordinal)));
    }
}
