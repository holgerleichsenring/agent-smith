using AgentSmith.Contracts.Models.Structure;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// What the commit gate found — the units over their Limits and not tolerated by a baseline, or
/// why it could not judge at all (<paramref name="Unavailable"/>: a grammar that would not load).
/// 2026-10-12-fbbda: only an excess fails it; a divergence is inside the band and is named in the
/// pull request instead.
/// </summary>
public sealed record StructureGateVerdict(IReadOnlyList<StructureFinding> Findings, string? Unavailable = null)
{
    public static StructureGateVerdict Pass { get; } = new([]);

    public IReadOnlyList<StructureFinding> Excess => [.. Findings.Where(f => f.IsExcess)];

    public IReadOnlyList<StructureFinding> Divergences => [.. Findings.Where(f => !f.IsExcess)];

    public bool Passed => Unavailable is null && Excess.Count == 0;
}
