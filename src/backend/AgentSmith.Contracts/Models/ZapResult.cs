namespace AgentSmith.Contracts.Models;

/// <summary>
/// Parsed output from an OWASP ZAP security scan. <see cref="Degraded"/> is set when the
/// scan ran but covered less than it was asked to (2026-08-30-26ed: it was cut off at its
/// time limit), so an empty result is readable as partial rather than as a clean target.
/// </summary>
public sealed record ZapResult(
    IReadOnlyList<ZapFinding> Findings,
    int DurationSeconds,
    string ScanType,
    int ExitCode = 0,
    bool Degraded = false,
    string? DegradedReason = null);

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
