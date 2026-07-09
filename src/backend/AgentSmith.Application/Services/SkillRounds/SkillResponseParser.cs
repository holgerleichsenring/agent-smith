using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SkillRounds;

/// <summary>
/// p0147d: Parses skill-round LLM responses into observations and applies the
/// confidence threshold (blocking + Confidence&lt;threshold → non-blocking;
/// p0167b: threshold is per-pipeline configurable, default 70). Also owns
/// the discussion-log text rendering shared across rounds.
/// </summary>
public sealed class SkillResponseParser(ObservationParser observationParser) : ISkillResponseParser
{
    public List<SkillObservation> ParseAndDowngrade(
        string responseText, string skillName, ILogger logger,
        IReadOnlyCollection<string>? readPaths = null,
        int confidenceThreshold = ResolvedPipelineConfig.DefaultConfidenceThreshold)
    {
        var raw = observationParser.ParseWithoutIds(responseText, skillName, logger, readPaths);
        var result = new List<SkillObservation>(raw.Count);
        foreach (var obs in raw)
        {
            if (obs.Blocking && obs.Confidence < confidenceThreshold)
            {
                logger.LogInformation(
                    "Skill {Skill}: blocking observation '{Concern}' downgraded to non-blocking (confidence {Confidence} < {Threshold})",
                    skillName, obs.Concern, obs.Confidence, confidenceThreshold);
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
