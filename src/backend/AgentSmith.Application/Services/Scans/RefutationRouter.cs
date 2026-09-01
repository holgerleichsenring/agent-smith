using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// 2026-09-01-85b2: decides which finding each row of a refuter's answer is ABOUT.
/// <para>
/// Routing used to be string equality on the displayed location. Two master findings on one
/// line — an authorization gap and an injection — share that string, so one refutation
/// silenced both; on the api path the location is often just the endpoint, where collisions
/// are the norm rather than the corner case. The call now carries an id per finding and the
/// answer echoes it.
/// </para>
/// <para>
/// A row that echoed no usable id is honoured only when its location names exactly ONE
/// candidate. Ambiguity answers nobody: the findings stand, which is the direction this
/// mechanism is allowed to fail in.
/// </para>
/// </summary>
public sealed class RefutationRouter(ILogger<RefutationRouter> logger)
{
    /// <summary>The answer about each candidate, keyed by the id the call carried.</summary>
    public IReadOnlyDictionary<string, FindingRefutation> Route(
        IReadOnlyList<CandidateFinding> candidates, IReadOnlyList<FindingRefutation> answers)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(answers);
        var routed = new Dictionary<string, FindingRefutation>(StringComparer.Ordinal);
        foreach (var answer in answers)
        {
            var target = Target(candidates, answer);
            if (target is not null) routed.TryAdd(target.Id, answer);
        }
        return routed;
    }

    private CandidateFinding? Target(
        IReadOnlyList<CandidateFinding> candidates, FindingRefutation answer)
    {
        var id = answer.Id?.Trim();
        var byId = string.IsNullOrEmpty(id)
            ? null
            : candidates.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
        if (byId is not null) return byId;

        var named = candidates
            .Where(c => string.Equals(c.Location, answer.Location?.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (named.Count == 1) return named[0];
        logger.LogWarning(
            "A refutation naming '{Location}' carries no usable finding id and matches "
            + "{Count} finding(s) — it answers none of them, and they all stand",
            answer.Location, named.Count);
        return null;
    }
}
