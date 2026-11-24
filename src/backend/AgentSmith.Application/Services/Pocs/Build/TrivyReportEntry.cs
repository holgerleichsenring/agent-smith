namespace AgentSmith.Application.Services.Pocs.Build;

/// <summary>
/// 2026-11-23-933dd: one finding of the build's compact Trivy report (poc-report.tpl) — a vulnerability (its id,
/// package, installed and fixed version, the V3Score of each CVSS source) or a secret (its rule id, target and line);
/// Trivy's severity for both.
/// </summary>
public sealed record TrivyReportEntry(
    string Kind, string Id, string? Package, string? Installed, string? Fixed, string Severity, IReadOnlyList<double> Scores,
    string? Title, string Target, int? Line)
{
    public const string VulnerabilityKind = "vulnerability";
    public const string SecretKind = "secret";
}
