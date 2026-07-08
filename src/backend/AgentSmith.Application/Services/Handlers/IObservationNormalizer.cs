using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Applies the normalization rules that turn a raw deserialised LLM observation
/// payload into a <see cref="SkillObservation"/>: confidence-scale migration
/// (1-10 → 0-100), per-field truncation against <c>ObservationCaps</c>, and
/// category-drift suppression (drop a Category that duplicates the Concern).
/// Lives in Application (not Contracts) because the rules log warnings, and
/// Contracts is a no-<c>ILogger</c> layer.
/// </summary>
public interface IObservationNormalizer
{
    /// <summary>
    /// Builds a typed observation from the supplied raw fields. The
    /// <paramref name="perRunWarn"/> set deduplicates per-role/per-field log
    /// warnings within a single parse pass.
    /// </summary>
    SkillObservation Normalize(
        RawObservationFields fields,
        string role,
        int id,
        HashSet<string> perRunWarn,
        ILogger? logger);
}

/// <summary>
/// Plain-data view of a deserialised LLM observation entry — the input shape
/// for <see cref="IObservationNormalizer"/>.
/// </summary>
public sealed record RawObservationFields(
    string? Concern,
    string Description,
    string? Suggestion,
    bool Blocking,
    string? Severity,
    int Confidence,
    string? Rationale,
    string? Effort,
    string? File,
    int StartLine,
    int? EndLine,
    string? ApiPath,
    string? SchemaName,
    string? EvidenceMode,
    string? ReviewStatus,
    string? Category,
    string? Details,
    string? LineRange = null);
