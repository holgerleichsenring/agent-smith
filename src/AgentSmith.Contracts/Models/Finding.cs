using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// A single finding from a security scan or analysis.
/// </summary>
public sealed record Finding(
    string Severity,
    string File,
    int StartLine,
    int? EndLine,
    string Title,
    string Description,
    int Confidence,
    string ReviewStatus = "not_reviewed",  // confirmed | false_positive | not_reviewed
    string? ApiPath = null,                // e.g. "POST /api/auth/login" — for API scan findings
    string? SchemaName = null,             // e.g. "OktaProcessInfoResponse" — for schema-level findings
    string Category = "unknown",           // e.g. "secrets", "injection", "dependencies"
    EvidenceMode EvidenceMode = EvidenceMode.Potential) // p79: confirmed only when backed by http_probe
{
    /// <summary>
    /// Returns the best available location string for display.
    /// AnalyzedFromSource always prefers File:StartLine; otherwise prefers
    /// ApiPath, then SchemaName, then File:StartLine.
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
