using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Builds a <see cref="SkillObservation"/> from a deserialised
/// <see cref="RawObservation"/>. Applies confidence migration
/// (1-10 → 0-100), field truncation (description/suggestion/rationale/details
/// capped at <see cref="ObservationCaps"/>), and category-drift suppression
/// (a Category that duplicates Concern is dropped). Split out of
/// ObservationParser to keep both files within the 120-line cap.
/// </summary>
internal static class RawObservationMapper
{
    internal static SkillObservation Build(
        RawObservation entry, string role, int id,
        HashSet<string> perRunWarn, ILogger? logger)
    {
        var confidence = ApplyConfidenceMigration(entry, role, perRunWarn, logger);
        var category = ApplyCategoryDriftCheck(entry, role, perRunWarn, logger);
        return new SkillObservation(
            Id: id, Role: role, Concern: entry.Concern,
            Description: Truncate(entry.Description, ObservationCaps.DescriptionMaxChars, role, "description", perRunWarn, logger) ?? "",
            Suggestion: Truncate(entry.Suggestion, ObservationCaps.SuggestionMaxChars, role, "suggestion", perRunWarn, logger) ?? "",
            Blocking: entry.Blocking, Severity: entry.Severity, Confidence: confidence,
            Rationale: Truncate(entry.Rationale, ObservationCaps.RationaleMaxChars, role, "rationale", perRunWarn, logger),
            Effort: entry.Effort,
            File: entry.File, StartLine: entry.StartLine, EndLine: entry.EndLine,
            ApiPath: entry.ApiPath, SchemaName: entry.SchemaName,
            EvidenceMode: entry.EvidenceMode ?? EvidenceMode.Potential,
            ReviewStatus: entry.ReviewStatus ?? "not_reviewed",
            Category: category,
            Details: Truncate(entry.Details, ObservationCaps.DetailsMaxChars, role, "details", perRunWarn, logger));
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

    private static string? Truncate(
        string? value, int maxChars, string role, string field,
        HashSet<string> perRunWarn, ILogger? logger)
    {
        if (value is null) return null;
        if (value.Length <= maxChars) return value;
        var marker = $"…[truncated, original was {value.Length} chars]";
        if (marker.Length >= maxChars) marker = "…[truncated]";
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
}
