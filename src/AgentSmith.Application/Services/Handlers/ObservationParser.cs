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

    /// <summary>
    /// Parses observations with placeholder Id=0 on every entry. Real IDs are assigned later
    /// at merge time (see ApplyBufferToContext) so deterministic ordering is preserved across
    /// parallel skill rounds.
    /// </summary>
    internal static List<SkillObservation> ParseWithoutIds(
        string response, string role, ILogger? logger = null)
    {
        var parsed = Parse(response, role, 0, logger);
        for (var i = 0; i < parsed.Count; i++)
            parsed[i] = parsed[i] with { Id = 0 };
        return parsed;
    }

    internal static List<SkillObservation> Parse(
        string response, string role, int startId, ILogger? logger = null)
    {
        var json = ExtractJsonArray(response);
        if (json is null)
            return FallbackSingle(response, role, startId, logger);

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return FallbackSingle(response, role, startId, logger);

            var result = new List<SkillObservation>();
            var totalElements = 0;
            var skippedFormat = 0;
            var id = startId;

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                totalElements++;
                var observation = TryBuildObservation(element, role, id, totalElements - 1, logger);
                if (observation is null) { skippedFormat++; continue; }
                result.Add(observation);
                id++;
            }

            if (result.Count == 0)
                return FallbackSingle(response, role, startId, logger);

            if (skippedFormat > 0)
                logger?.LogWarning(
                    "Parsed {Valid}/{Total} observations from {Role} — {Skipped} skipped due to invalid JSON shape",
                    result.Count, totalElements, role, skippedFormat);

            return result;
        }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex, "JSON parse failed for {Role}, falling back to single observation", role);
            return FallbackSingle(response, role, startId, logger);
        }
    }

    private static SkillObservation? TryBuildObservation(
        JsonElement element, string role, int id, int index, ILogger? logger)
    {
        try
        {
            var entry = element.Deserialize<RawObservation>(JsonOptions);
            if (entry is null || string.IsNullOrWhiteSpace(entry.Description))
                return null;
            return new SkillObservation(
                Id: id, Role: role, Concern: entry.Concern,
                Description: entry.Description, Suggestion: entry.Suggestion ?? "",
                Blocking: entry.Blocking, Severity: entry.Severity,
                Confidence: Math.Clamp(entry.Confidence, 0, 100),
                Rationale: entry.Rationale, Location: entry.Location, Effort: entry.Effort);
        }
        catch (JsonException ex)
        {
            var preview = element.GetRawText();
            if (preview.Length > 200) preview = preview[..200];
            logger?.LogWarning(
                "Skipping observation index {Index} from {Role} — invalid JSON shape: {Error}. Preview: {Preview}",
                index, role, ex.Message, preview);
            return null;
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
