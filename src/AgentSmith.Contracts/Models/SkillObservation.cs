namespace AgentSmith.Contracts.Models;

/// <summary>
/// A single typed observation produced by a skill agent or a scanner.
/// Universal pipeline carrier: replaces both free-text discussion entries
/// and the legacy Finding record. ID is assigned by the framework, not the LLM.
/// </summary>
public sealed record SkillObservation(
    int Id,
    string Role,
    ObservationConcern Concern,
    string Description,
    string Suggestion,
    bool Blocking,
    ObservationSeverity Severity,
    int Confidence,
    string? Rationale = null,
    ObservationEffort? Effort = null,
    string? File = null,
    int StartLine = 0,
    int? EndLine = null,
    string? ApiPath = null,
    string? SchemaName = null,
    EvidenceMode EvidenceMode = EvidenceMode.Potential,
    string ReviewStatus = "not_reviewed",
    string? Category = null)
{
    /// <summary>
    /// Best-available location string for display. Mirrors Finding.DisplayLocation
    /// from the pre-p0123 type, preserved here so output strategies render the same.
    /// </summary>
    public string DisplayLocation
    {
        get
        {
            if (EvidenceMode == EvidenceMode.AnalyzedFromSource
                && !string.IsNullOrWhiteSpace(File) && StartLine > 0)
                return $"{File}:{StartLine}";
            if (!string.IsNullOrWhiteSpace(ApiPath)) return ApiPath;
            if (!string.IsNullOrWhiteSpace(SchemaName)) return SchemaName;
            if (!string.IsNullOrWhiteSpace(File) && StartLine > 0) return $"{File}:{StartLine}";
            if (!string.IsNullOrWhiteSpace(File)) return File;
            return "General";
        }
    }
}
