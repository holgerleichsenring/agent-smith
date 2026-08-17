namespace AgentSmith.Contracts.Models;

/// <summary>
/// Parsed output from an OWASP ZAP security scan.
/// </summary>
public sealed record ZapResult(
    IReadOnlyList<ZapFinding> Findings,
    int DurationSeconds,
    string ScanType,
    int ExitCode = 0);

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
    int Count,
    /// <summary>p0429a: the instance ZAP actually sent and got back, when its report
    /// carried one — the evidence a refuter is shown for a live-target claim.</summary>
    HttpExchange? Exchange = null);
