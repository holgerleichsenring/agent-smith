using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Default <see cref="IObservationNormalizer"/>: applies confidence migration
/// (1-10 → 0-100), per-field truncation against <c>ObservationCaps</c>, and
/// drops a Category that duplicates the Concern. Each rule logs at most once
/// per (role, field) via the shared <c>perRunWarn</c> set.
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
        var confidence = MigrateConfidence(fields.Confidence, role, perRunWarn, logger);
        var category = DropDuplicateCategory(fields.Category, fields.Concern, role, perRunWarn, logger);
        return new SkillObservation(
            Id: id, Role: role, Concern: fields.Concern,
            Description: Truncate(fields.Description, ObservationCaps.DescriptionMaxChars, role, "description", perRunWarn, logger) ?? "",
            Suggestion: Truncate(fields.Suggestion, ObservationCaps.SuggestionMaxChars, role, "suggestion", perRunWarn, logger) ?? "",
            Blocking: fields.Blocking, Severity: fields.Severity, Confidence: confidence,
            Rationale: Truncate(fields.Rationale, ObservationCaps.RationaleMaxChars, role, "rationale", perRunWarn, logger),
            Effort: fields.Effort,
            File: fields.File, StartLine: fields.StartLine, EndLine: fields.EndLine,
            ApiPath: fields.ApiPath, SchemaName: fields.SchemaName,
            EvidenceMode: fields.EvidenceMode ?? Contracts.Models.EvidenceMode.Potential,
            ReviewStatus: fields.ReviewStatus ?? "not_reviewed",
            Category: category,
            Details: Truncate(fields.Details, ObservationCaps.DetailsMaxChars, role, "details", perRunWarn, logger));
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
