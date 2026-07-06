namespace AgentSmith.Contracts.Models;

/// <summary>
/// Parsed output from a Nuclei security scan. <see cref="Degraded"/> is set when the
/// scan ran but with reduced coverage (e.g. swagger parse failure → base URL only), so
/// the operator sees a partial scan instead of a falsely-clean result.
/// </summary>
public sealed record NucleiResult(
    IReadOnlyList<NucleiFinding> Findings,
    int DurationSeconds,
    string RawOutput,
    bool Degraded = false,
    string? DegradedReason = null);

public sealed record NucleiFinding(
    string TemplateId,
    string Name,
    string Severity,
    string MatchedUrl,
    string? Description,
    string? Reference);
