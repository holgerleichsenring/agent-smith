namespace AgentSmith.Contracts.Models;

/// <summary>
/// Parsed output from a Nuclei security scan.
/// </summary>
public sealed record NucleiResult(
    IReadOnlyList<NucleiFinding> Findings,
    int DurationSeconds,
    string RawOutput);

public sealed record NucleiFinding(
    string TemplateId,
    string Name,
    string Severity,
    string MatchedUrl,
    string? Description,
    string? Reference);
