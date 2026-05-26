namespace AgentSmith.Contracts.Models;

/// <summary>
/// Parsed output from a Spectral OpenAPI lint run.
/// </summary>
public sealed record SpectralResult(
    IReadOnlyList<SpectralFinding> Findings,
    int ErrorCount,
    int WarnCount,
    int DurationSeconds);

public sealed record SpectralFinding(
    string Code,
    string Message,
    string Path,
    string Severity,
    int Line);
