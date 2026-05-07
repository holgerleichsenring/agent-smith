using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Parses LLM JSON responses into SkillObservation lists.
/// Element-wise: bad observations are skipped with a warning, valid ones survive.
/// Migration helpers: legacy Location string is split into structured fields;
/// 1-10 confidence values are auto-upgraded to 0-100; Category that duplicates
/// Concern is dropped with a warning.
/// </summary>
internal static class ObservationParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly Regex FileLineRegex =
        new(@"^(?<file>[^\s].*?):(?<line>\d+)$", RegexOptions.Compiled);
    private static readonly Regex HttpEndpointRegex =
        new(@"^(GET|POST|PUT|PATCH|DELETE|HEAD|OPTIONS)\s+/.+", RegexOptions.Compiled);
    private static readonly Regex SchemaNameRegex =
        new(@"^[A-Z][A-Za-z0-9_]*$", RegexOptions.Compiled);

    private static readonly HashSet<string> WarnedSeverityValues = new(StringComparer.OrdinalIgnoreCase);

    internal static List<SkillObservation> ParseWithoutIds(
        string response, string role, ILogger? logger = null)
    {
        var parsed = Parse(response, role, 0, logger);
        for (var i = 0; i < parsed.Count; i++)
            parsed[i] = parsed[i] with { Id = 0 };
        return parsed;
    }

    /// <summary>
    /// Strict variant: returns null when the response can't be parsed AND the resilient
    /// fallback also yields zero. Callers like FilterRoundHandler use this so they can
    /// detect true full-failure and preserve the existing observation list instead of
    /// overwriting it with a single auto-wrapped placeholder.
    ///
    /// Two-layer parsing:
    /// 1. Strict: ExtractJsonArray + JsonDocument.Parse + per-element TryBuildObservation.
    ///    Fast path for well-formed responses.
    /// 2. Resilient (on JsonException or no array bracket): ResilientJsonObjectExtractor
    ///    finds complete object literals via brace-counting. Recovers from truncated-mid-array.
    /// </summary>
    internal static List<SkillObservation>? TryParseWithoutIds(
        string response, string role, ILogger? logger = null)
    {
        var json = ExtractJsonArray(response);
        if (json is null) return TryResilientFallback(response, role, logger);

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return TryResilientFallback(response, role, logger);

            var result = new List<SkillObservation>();
            var perRunWarn = new HashSet<string>();
            var index = 0;
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var observation = TryBuildObservation(element, role, 0, index, perRunWarn, logger);
                if (observation is not null) result.Add(observation);
                index++;
            }
            return result.Count == 0 ? null : result;
        }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex,
                "Strict JSON parse failed for {Role}; falling through to resilient extraction",
                role);
            return TryResilientFallback(response, role, logger);
        }
    }

    private static List<SkillObservation>? TryResilientFallback(
        string response, string role, ILogger? logger)
    {
        var result = new List<SkillObservation>();
        var perRunWarn = new HashSet<string>();
        var index = 0;
        foreach (var objectLiteral in ResilientJsonObjectExtractor.ExtractObjects(response))
        {
            try
            {
                using var doc = JsonDocument.Parse(objectLiteral);
                var observation = TryBuildObservation(doc.RootElement, role, 0, index, perRunWarn, logger);
                if (observation is not null) result.Add(observation);
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
            var perRunWarn = new HashSet<string>();

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                totalElements++;
                var observation = TryBuildObservation(element, role, id, totalElements - 1, perRunWarn, logger);
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
        JsonElement element, string role, int id, int index,
        HashSet<string> perRunWarn, ILogger? logger)
    {
        try
        {
            var entry = element.Deserialize<RawObservation>(JsonOptions);
            if (entry is null || string.IsNullOrWhiteSpace(entry.Description))
                return null;

            ApplyLocationMigration(entry);
            var confidence = ApplyConfidenceMigration(entry, role, perRunWarn, logger);
            var category = ApplyCategoryDriftCheck(entry, role, perRunWarn, logger);

            return new SkillObservation(
                Id: id, Role: role, Concern: entry.Concern,
                Description: TruncateField(entry.Description, ObservationCaps.DescriptionMaxChars, role, "description", perRunWarn, logger) ?? "",
                Suggestion: TruncateField(entry.Suggestion, ObservationCaps.SuggestionMaxChars, role, "suggestion", perRunWarn, logger) ?? "",
                Blocking: entry.Blocking, Severity: entry.Severity,
                Confidence: confidence,
                Rationale: TruncateField(entry.Rationale, ObservationCaps.RationaleMaxChars, role, "rationale", perRunWarn, logger),
                Effort: entry.Effort,
                File: entry.File, StartLine: entry.StartLine, EndLine: entry.EndLine,
                ApiPath: entry.ApiPath, SchemaName: entry.SchemaName,
                EvidenceMode: entry.EvidenceMode ?? EvidenceMode.Potential,
                ReviewStatus: entry.ReviewStatus ?? "not_reviewed",
                Category: category,
                Details: TruncateField(entry.Details, ObservationCaps.DetailsMaxChars, role, "details", perRunWarn, logger));
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

    private static void ApplyLocationMigration(RawObservation entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Location)) return;
        if (!string.IsNullOrEmpty(entry.File)
            || !string.IsNullOrEmpty(entry.ApiPath)
            || !string.IsNullOrEmpty(entry.SchemaName)) return;

        var loc = entry.Location.Trim();
        var fileLineMatch = FileLineRegex.Match(loc);
        if (fileLineMatch.Success && int.TryParse(fileLineMatch.Groups["line"].Value, out var line))
        {
            entry.File = fileLineMatch.Groups["file"].Value;
            entry.StartLine = line;
            return;
        }
        if (HttpEndpointRegex.IsMatch(loc)) { entry.ApiPath = loc; return; }
        if (SchemaNameRegex.IsMatch(loc)) { entry.SchemaName = loc; return; }
        entry.File = loc;
    }

    private static int ApplyConfidenceMigration(
        RawObservation entry, string role, HashSet<string> perRunWarn, ILogger? logger)
    {
        var raw = entry.Confidence;
        if (raw <= 0) return 0;
        if (raw > 10) return Math.Clamp(raw, 0, 100);
        var key = $"confidence-1-10:{role}";
        if (perRunWarn.Add(key))
            logger?.LogWarning(
                "Skill {Role} emitted confidence on 1-10 scale; auto-migrated to 0-100. Update SKILL.md to use 0-100 explicitly.",
                role);
        return Math.Clamp(raw * 10, 0, 100);
    }

    private static string? TruncateField(
        string? value, int maxChars, string role, string field,
        HashSet<string> perRunWarn, ILogger? logger)
    {
        if (value is null) return null;
        if (value.Length <= maxChars) return value;

        var marker = $"…[truncated, original was {value.Length} chars]";
        // Ensure marker fits within cap; truncate the marker itself if cap is unusually small.
        if (marker.Length >= maxChars)
            marker = "…[truncated]";
        var headRoom = Math.Max(0, maxChars - marker.Length);
        var truncated = value[..headRoom] + marker;

        var key = $"truncate:{role}:{field}";
        if (perRunWarn.Add(key))
            logger?.LogWarning(
                "Skill {Role}: '{Field}' truncated from {Original} to {Cap} chars. Use 'details' for long-form prose.",
                role, field, value.Length, maxChars);
        return truncated;
    }

    private static string? ApplyCategoryDriftCheck(
        RawObservation entry, string role, HashSet<string> perRunWarn, ILogger? logger)
    {
        if (string.IsNullOrWhiteSpace(entry.Category)) return null;
        var concernText = entry.Concern.ToString();
        if (!entry.Category.Equals(concernText, StringComparison.OrdinalIgnoreCase))
            return entry.Category;
        var key = $"category-duplicates-concern:{role}:{entry.Category}";
        if (perRunWarn.Add(key))
            logger?.LogWarning(
                "{Role}: Category '{Category}' duplicates Concern; pick a finer-grained tag (e.g. 'secrets', 'injection', 'auth' for Security).",
                role, entry.Category);
        return null;
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
    /// DTO matching the LLM's JSON output. Includes legacy Location for backward
    /// compatibility and all new typed fields. Mutable so the migration helpers
    /// can fill File/StartLine/etc. from the legacy Location before construction.
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
        public string? File { get; set; }
        public int StartLine { get; set; }
        public int? EndLine { get; set; }
        public string? ApiPath { get; set; }
        public string? SchemaName { get; set; }
        public EvidenceMode? EvidenceMode { get; set; }
        public string? ReviewStatus { get; set; }
        public string? Category { get; set; }
        public string? Details { get; set; }
    }
}
