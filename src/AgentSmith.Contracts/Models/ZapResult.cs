namespace AgentSmith.Contracts.Models;

/// <summary>
/// Parsed output from an OWASP ZAP security scan.
/// </summary>
public sealed record ZapResult(
    IReadOnlyList<ZapFinding> Findings,
    int DurationSeconds,
    string ScanType);

public sealed record ZapFinding(
    string AlertRef,
    string Name,
    string RiskDescription,
    string Confidence,
    string Url,
    string Description,
    string? Solution,
    string? CweId,
    string? WascId,
    int Count);
