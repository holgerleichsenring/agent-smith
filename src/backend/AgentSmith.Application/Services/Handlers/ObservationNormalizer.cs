using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Default <see cref="IObservationNormalizer"/>: applies confidence migration
/// (1-10 → 0-100), per-field truncation against <c>ObservationCaps</c>, drops
/// a Category that duplicates the Concern, and tolerantly parses the four
/// enum-shaped string fields the LLM emits (concern, severity, effort,
/// evidence_mode). Unknown values default to a documented fallback and log
/// once per (role, field) via the shared <c>perRunWarn</c> set.
/// </summary>
public sealed class ObservationNormalizer : IObservationNormalizer
{
    public SkillObservation Normalize(
        RawObservationFields fields,
        string role,
        int id,
        HashSet<string> perRunWarn,
        ILogger? logger)
    {
        var concern = ParseConcern(fields.Concern, role, perRunWarn, logger);
        var severity = ParseSeverity(fields.Severity, role, perRunWarn, logger);
        var effort = ParseEffort(fields.Effort, role, perRunWarn, logger);
        var evidenceMode = ParseEvidenceMode(fields.EvidenceMode, role, perRunWarn, logger);
        var confidence = MigrateConfidence(fields.Confidence, role, perRunWarn, logger);
        var category = DropDuplicateCategory(fields.Category, concern, role, perRunWarn, logger);
        var lineRange = ParseLineRange(fields.LineRange, role, perRunWarn, logger);
        return new SkillObservation(
            Id: id, Role: role, Concern: concern,
            Description: Truncate(fields.Description, ObservationCaps.DescriptionMaxChars, role, "description", perRunWarn, logger) ?? "",
            Suggestion: Truncate(fields.Suggestion, ObservationCaps.SuggestionMaxChars, role, "suggestion", perRunWarn, logger) ?? "",
            Blocking: fields.Blocking, Severity: severity, Confidence: confidence,
            Rationale: Truncate(fields.Rationale, ObservationCaps.RationaleMaxChars, role, "rationale", perRunWarn, logger),
            Effort: effort,
            File: fields.File,
            StartLine: fields.StartLine == 0 && lineRange is not null ? lineRange.Start : fields.StartLine,
            EndLine: fields.StartLine == 0 && fields.EndLine is null && lineRange is not null
                ? lineRange.End
                : fields.EndLine,
            ApiPath: fields.ApiPath, SchemaName: fields.SchemaName,
            EvidenceMode: evidenceMode,
            ReviewStatus: fields.ReviewStatus ?? "not_reviewed",
            Category: category,
            Details: Truncate(fields.Details, ObservationCaps.DetailsMaxChars, role, "details", perRunWarn, logger),
            LineRange: lineRange);
    }

    private static ObservationLineRange? ParseLineRange(
        string? raw, string role, HashSet<string> perRunWarn, ILogger? logger)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var parsed = ObservationLineRange.Parse(raw);
        if (parsed is null && perRunWarn.Add($"line-range:{role}:{raw}"))
            logger?.LogWarning(
                "Skill {Role}: unparseable line_range '{Raw}' — expected 'start..end'. Dropped the range, kept the observation.",
                role, raw);
        return parsed;
    }

    private static ObservationConcern ParseConcern(string? raw, string role, HashSet<string> perRunWarn, ILogger? logger) =>
        TryParseEnum(raw, ObservationConcern.Correctness, role, "concern", perRunWarn, logger);

    private static ObservationSeverity ParseSeverity(string? raw, string role, HashSet<string> perRunWarn, ILogger? logger) =>
        TryParseEnum(raw, ObservationSeverity.Info, role, "severity", perRunWarn, logger);

    private static ObservationEffort? ParseEffort(string? raw, string role, HashSet<string> perRunWarn, ILogger? logger)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (Enum.TryParse<ObservationEffort>(raw, ignoreCase: true, out var parsed)) return parsed;
        WarnUnknownEnum(raw, role, "effort", typeof(ObservationEffort), perRunWarn, logger);
        return null;
    }

    private static EvidenceMode ParseEvidenceMode(string? raw, string role, HashSet<string> perRunWarn, ILogger? logger) =>
        TryParseEnum(raw, EvidenceMode.Potential, role, "evidence_mode", perRunWarn, logger);

    private static T TryParseEnum<T>(
        string? raw, T fallback, string role, string field,
        HashSet<string> perRunWarn, ILogger? logger) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        // The codebase is snake_case end-to-end (YAML, SKILL.md, JSON properties).
        // LLM emits enum values the same way: "analyzed_from_source" matches the
        // PascalCase enum name "AnalyzedFromSource" once underscores are stripped.
        var normalized = raw.Replace("_", "").Replace("-", "");
        if (Enum.TryParse<T>(normalized, ignoreCase: true, out var parsed)) return parsed;
        WarnUnknownEnum(raw, role, field, typeof(T), perRunWarn, logger);
        return fallback;
    }

    private static void WarnUnknownEnum(
        string raw, string role, string field, Type enumType,
        HashSet<string> perRunWarn, ILogger? logger)
    {
        if (!perRunWarn.Add($"unknown-enum:{role}:{field}:{raw}")) return;
        var allowed = string.Join(", ", Enum.GetNames(enumType));
        logger?.LogWarning(
            "Skill {Role}: unknown {Field} value '{Raw}' — defaulted. Allowed: [{Allowed}].",
            role, field, raw, allowed);
    }

    private static int MigrateConfidence(int raw, string role, HashSet<string> perRunWarn, ILogger? logger)
    {
        if (raw <= 0) return 0;
        if (raw > 10) return Math.Clamp(raw, 0, 100);
        if (perRunWarn.Add($"confidence-1-10:{role}"))
            logger?.LogWarning(
                "Skill {Role} emitted confidence on 1-10 scale; auto-migrated to 0-100. Update SKILL.md to use 0-100 explicitly.",
                role);
        return Math.Clamp(raw * 10, 0, 100);
    }

    private static string? Truncate(
        string? value, int maxChars, string role, string field,
        HashSet<string> perRunWarn, ILogger? logger)
    {
        if (value is null || value.Length <= maxChars) return value;
        var marker = $"…[truncated, original was {value.Length} chars]";
        if (marker.Length >= maxChars) marker = "…[truncated]";
        var headRoom = Math.Max(0, maxChars - marker.Length);
        if (perRunWarn.Add($"truncate:{role}:{field}"))
            logger?.LogWarning(
                "Skill {Role}: '{Field}' truncated from {Original} to {Cap} chars. Use 'details' for long-form prose.",
                role, field, value.Length, maxChars);
        return value[..headRoom] + marker;
    }

    private static string? DropDuplicateCategory(
        string? category, ObservationConcern concern, string role,
        HashSet<string> perRunWarn, ILogger? logger)
    {
        if (string.IsNullOrWhiteSpace(category)) return null;
        if (!category.Equals(concern.ToString(), StringComparison.OrdinalIgnoreCase)) return category;
        if (perRunWarn.Add($"category-duplicates-concern:{role}:{category}"))
            logger?.LogWarning(
                "{Role}: Category '{Category}' duplicates Concern; pick a finer-grained tag (e.g. 'secrets', 'injection', 'auth' for Security).",
                role, category);
        return null;
    }
}
