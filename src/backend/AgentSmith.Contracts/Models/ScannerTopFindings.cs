namespace AgentSmith.Contracts.Models;

/// <summary>
/// p0151g: structured top-N of each scanner's highest-confidence findings,
/// preserved alongside the prose summary so a downstream skill (e.g. the
/// scanner-correlation-judge in p0151f) can cite specific template_ids and
/// matched URLs rather than re-deriving them from prose. Selection policy
/// is deterministic — the same scanner output produces the same top-N.
/// </summary>
public sealed record ScannerTopFindings(
    IReadOnlyList<NucleiFinding> Nuclei,
    IReadOnlyList<ZapFinding> Zap,
    IReadOnlyList<SpectralFinding> Spectral)
{
    public static ScannerTopFindings Empty { get; } = new(
        Array.Empty<NucleiFinding>(),
        Array.Empty<ZapFinding>(),
        Array.Empty<SpectralFinding>());

    public int TotalCount => Nuclei.Count + Zap.Count + Spectral.Count;
}
