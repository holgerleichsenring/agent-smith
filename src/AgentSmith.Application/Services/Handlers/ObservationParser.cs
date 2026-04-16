using System.Text.Json;
using System.Text.Json.Serialization;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Parses LLM JSON responses into SkillObservation lists.
/// Handles partial valid JSON: takes valid observations, skips broken ones.
/// Falls back to a single observation wrapping raw text if nothing is parseable.
/// </summary>
internal static class ObservationParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    internal static List<SkillObservation> Parse(
        string response, string role, int startId, ILogger? logger = null)
    {
        var json = ExtractJsonArray(response);
        if (json is null)
            return FallbackSingle(response, role, startId, logger);

        try
        {
            var raw = JsonSerializer.Deserialize<List<RawObservation>>(json, JsonOptions);
            if (raw is null || raw.Count == 0)
                return FallbackSingle(response, role, startId, logger);

            var result = new List<SkillObservation>();
            var id = startId;

            foreach (var entry in raw)
            {
                if (string.IsNullOrWhiteSpace(entry.Description))
                {
                    logger?.LogWarning("Skipping observation with empty description from {Role}", role);
                    continue;
                }

                result.Add(new SkillObservation(
                    Id: id++,
                    Role: role,
                    Concern: entry.Concern,
                    Description: entry.Description,
                    Suggestion: entry.Suggestion ?? "",
                    Blocking: entry.Blocking,
                    Severity: entry.Severity,
                    Confidence: Math.Clamp(entry.Confidence, 0, 100),
                    Rationale: entry.Rationale,
                    Location: entry.Location,
                    Effort: entry.Effort));
            }

            if (result.Count == 0)
                return FallbackSingle(response, role, startId, logger);

            if (result.Count < raw.Count)
                logger?.LogWarning(
                    "Parsed {Valid}/{Total} observations from {Role} — {Skipped} skipped",
                    result.Count, raw.Count, role, raw.Count - result.Count);

            return result;
        }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex, "JSON parse failed for {Role}, falling back to single observation", role);
            return FallbackSingle(response, role, startId, logger);
        }
    }

    private static List<SkillObservation> FallbackSingle(
        string response, string role, int startId, ILogger? logger)
    {
        logger?.LogWarning("Could not parse observations from {Role}, wrapping as single observation", role);
        return
        [
            new SkillObservation(
                Id: startId,
                Role: role,
                Concern: ObservationConcern.Correctness,
                Description: response.Length > 2000 ? response[..2000] : response,
                Suggestion: "",
                Blocking: false,
                Severity: ObservationSeverity.Info,
                Confidence: 50,
                Rationale: "Auto-wrapped: LLM did not return structured observations")
        ];
    }

    private static string? ExtractJsonArray(string response)
    {
        // Strip markdown fences if present
        var text = response.Trim();
        if (text.StartsWith("```"))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline > 0) text = text[(firstNewline + 1)..];
            if (text.EndsWith("```")) text = text[..^3];
            text = text.Trim();
        }

        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        if (start >= 0 && end > start)
            return text[start..(end + 1)];

        return null;
    }

    /// <summary>
    /// DTO matching the LLM's JSON output (no Id — framework assigns IDs).
    /// </summary>
    private sealed class RawObservation
    {
        public ObservationConcern Concern { get; set; }
        public string Description { get; set; } = "";
        public string? Suggestion { get; set; }
        public bool Blocking { get; set; }
        public ObservationSeverity Severity { get; set; }
        public int Confidence { get; set; }
        public string? Rationale { get; set; }
        public string? Location { get; set; }
        public ObservationEffort? Effort { get; set; }
    }
}
