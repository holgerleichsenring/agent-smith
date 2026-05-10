using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0129a: renders verify-phase observations into a human-readable note block
/// that flows back into AgenticExecute on re-loop and into the ticket writeback
/// on escalation. Format: round header + one bulleted line per blocking observation
/// (non-blocking ones are recorded in VerifyObservations but kept out of the notes
/// to keep the implementer's re-prompt focused).
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

    public static string Format(int round, IReadOnlyList<SkillObservation> observations)
    {
        var blocking = observations.Where(o => o.Blocking).ToList();
        if (blocking.Count == 0) return $"## Verify round {round}: no blocking observations";

        var lines = blocking.Select(o =>
        {
            var location = o.DisplayLocation;
            var loc = location == "General" ? "" : $" ({location})";
            return $"- [{o.Severity}] **{o.Concern}**{loc}: {o.Description}" +
                   (string.IsNullOrWhiteSpace(o.Suggestion) ? "" : $" → {o.Suggestion}");
        });
        return $"## Verify round {round}: {blocking.Count} blocking observation(s)\n\n" +
               string.Join("\n", lines);
    }
}
