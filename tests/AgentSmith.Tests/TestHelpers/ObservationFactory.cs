using AgentSmith.Contracts.Models;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// Compact constructors for SkillObservation in tests. Mirrors the old Finding
/// shape (severity, file, line, title, description, confidence) so existing
/// tests migrate by name with minimal churn.
/// </summary>
internal static class ObservationFactory
{
    internal static SkillObservation Make(
        string severity, string file, int line, string title, string description,
        int confidence, string category = "unknown",
        string reviewStatus = "not_reviewed",
        EvidenceMode evidence = EvidenceMode.AnalyzedFromSource,
        string? apiPath = null, string? schemaName = null) =>
        new(
            Id: 0, Role: "test",
            Concern: ObservationConcern.Security,
            Description: string.IsNullOrEmpty(description) ? title : $"{title}\n{description}",
            Suggestion: "",
            Blocking: false,
            Severity: ParseSeverity(severity),
            Confidence: confidence > 10 ? confidence : confidence * 10,
            File: file, StartLine: line,
            ApiPath: apiPath, SchemaName: schemaName,
            EvidenceMode: evidence,
            ReviewStatus: reviewStatus,
            Category: category);

    private static ObservationSeverity ParseSeverity(string s) => s.ToUpperInvariant() switch
    {
        "CRITICAL" or "HIGH" => ObservationSeverity.High,
        "MEDIUM" => ObservationSeverity.Medium,
        "LOW" => ObservationSeverity.Low,
        _ => ObservationSeverity.Info,
    };
}
