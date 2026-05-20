using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Resilient + total-fallback recovery paths used by <see cref="ObservationParser"/>.
/// Split out to keep ObservationParser within the 120-line size cap. Brace-counted
/// object literal recovery and the auto-wrapped single-observation fallback live
/// here; the typed array → SkillObservation mapping stays on ObservationParser.
/// </summary>
internal static class ObservationRecoveryHelper
{
    internal static List<SkillObservation>? TryResilientFallback(
        ITolerantJsonParser tolerantParser, string response, string role,
        Func<JsonElement, int, HashSet<string>, ILogger?, SkillObservation?> tryBuild,
        ILogger? logger)
    {
        var result = new List<SkillObservation>();
        var perRunWarn = new HashSet<string>();
        var index = 0;
        foreach (var literal in tolerantParser.ExtractArrayObjects(response))
        {
            try
            {
                using var doc = JsonDocument.Parse(literal);
                var obs = tryBuild(doc.RootElement, index, perRunWarn, logger);
                if (obs is not null) result.Add(obs);
            }
            catch (JsonException) { /* malformed object literal — skip */ }
            index++;
        }
        if (result.Count > 0)
            logger?.LogWarning(
                "Resilient extraction recovered {Count} observations from truncated/malformed response for {Role}",
                result.Count, role);
        return result.Count == 0 ? null : result;
    }

    internal static List<SkillObservation> FallbackSingle(
        string response, string role, int startId, ILogger? logger)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            logger?.LogWarning(
                "Skill {Role} returned an empty response; no observations to parse", role);
            return
            [
                new SkillObservation(
                    Id: startId, Role: role, Concern: ObservationConcern.Correctness,
                    Description: $"Skill '{role}' returned an empty response — no observations could be parsed.",
                    Suggestion: "", Blocking: false,
                    Severity: ObservationSeverity.Info, Confidence: 0,
                    Rationale: "Auto-wrapped: LLM returned no content",
                    Category: ExecutionLimitCategories.ExecutionParseFailure)
            ];
        }
        logger?.LogWarning("Could not parse observations from {Role}, wrapping as single observation", role);
        return
        [
            new SkillObservation(
                Id: startId, Role: role, Concern: ObservationConcern.Correctness,
                Description: response.Length > 2000 ? response[..2000] : response,
                Suggestion: "", Blocking: false,
                Severity: ObservationSeverity.Info, Confidence: 50,
                Rationale: "Auto-wrapped: LLM did not return structured observations")
        ];
    }
}
