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
