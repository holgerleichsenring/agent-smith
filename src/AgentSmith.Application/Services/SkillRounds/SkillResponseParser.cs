using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SkillRounds;

/// <summary>
/// p0147d: Parses skill-round LLM responses into observations and applies the
/// confidence threshold (blocking + Confidence&lt;70 → non-blocking). Also owns
/// the discussion-log text rendering shared across rounds.
/// </summary>
public sealed class SkillResponseParser : ISkillResponseParser
{
    public List<SkillObservation> ParseAndDowngrade(string responseText, string skillName, ILogger logger)
    {
        var raw = ObservationParser.ParseWithoutIds(responseText, skillName, logger);
        var result = new List<SkillObservation>(raw.Count);
        foreach (var obs in raw)
        {
            if (obs.Blocking && obs.Confidence < 70)
            {
                logger.LogInformation(
                    "Skill {Skill}: blocking observation '{Concern}' downgraded to non-blocking (confidence {Confidence} < 70)",
                    skillName, obs.Concern, obs.Confidence);
                result.Add(obs with { Blocking = false });
            }
            else result.Add(obs);
        }
        return result;
    }

    public string RenderObservationsAsText(IReadOnlyList<SkillObservation> observations)
    {
        if (observations.Count == 0) return "No observations.";
        return string.Join("\n\n", observations.Select(o =>
        {
            var blocking = o.Blocking ? " [BLOCKING]" : "";
            var loc = o.DisplayLocation;
            var location = loc != "General" ? $" ({loc})" : "";
            return $"**{o.Concern}{blocking}** [{o.Severity}] (confidence: {o.Confidence}){location}\n"
                 + $"{o.Description}\n"
                 + (string.IsNullOrWhiteSpace(o.Suggestion) ? "" : $"→ {o.Suggestion}");
        }));
    }
}
