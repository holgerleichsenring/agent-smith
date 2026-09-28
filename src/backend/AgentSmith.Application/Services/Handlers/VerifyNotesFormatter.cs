using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Collapses verify-phase observations that repeat across rounds.
/// </summary>
internal static class VerifyNotesFormatter
{
    private const int DescriptionPrefixLength = 100;

    /// <summary>
    /// p0129c: collapse cross-round duplicates by (file, concern, description-prefix-100).
    /// Round-2 verifiers tend to re-emit identical observations on unfixed problems;
    /// the writeback should show one entry per unique concern rather than two.
    /// Within a single round there are no duplicates, so single-round callers can skip.
    /// Last-emitted wins so the most recent rationale survives.
    /// </summary>
    public static IReadOnlyList<SkillObservation> Dedup(IEnumerable<SkillObservation> observations)
    {
        var seen = new Dictionary<(string File, string Concern, string DescPrefix), SkillObservation>();
        foreach (var obs in observations)
        {
            var prefix = obs.Description.Length <= DescriptionPrefixLength
                ? obs.Description
                : obs.Description[..DescriptionPrefixLength];
            var key = (obs.File ?? string.Empty, obs.Concern.ToString(), prefix);
            seen[key] = obs;
        }
        return seen.Values.ToList();
    }
}
