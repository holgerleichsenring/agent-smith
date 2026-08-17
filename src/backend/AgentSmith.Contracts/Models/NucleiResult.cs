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
    string? Reference,
    /// <summary>p0429a: present only when Nuclei was asked to emit the request/response
    /// pair; absent is normal and leaves the finding un-refuted rather than refuted on
    /// evidence nobody has.</summary>
    HttpExchange? Exchange = null);
