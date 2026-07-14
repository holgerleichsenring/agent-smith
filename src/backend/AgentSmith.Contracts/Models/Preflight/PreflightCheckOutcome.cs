namespace AgentSmith.Contracts.Models.Preflight;

/// <summary>
/// p0324: one aggregated runner entry — the check's identity plus its result and
/// wall-clock duration (active probes are network round-trips; the duration tells
/// the operator whether a pass was snappy or borderline).
/// </summary>
public sealed record PreflightCheckOutcome(
    string Name,
    string Category,
    PreflightCheckResult Result,
    long DurationMs);
