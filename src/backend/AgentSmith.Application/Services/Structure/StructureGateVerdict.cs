using AgentSmith.Contracts.Models.Structure;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-06-8565d: what the commit gate found — the units newly over their Limits, or why it
/// could not judge at all (<paramref name="Unavailable"/>: a grammar that would not load).
/// </summary>
public sealed record StructureGateVerdict(IReadOnlyList<StructureFinding> Findings, string? Unavailable = null)
{
    public static StructureGateVerdict Pass { get; } = new([]);

    public bool Passed => Unavailable is null && Findings.Count == 0;
}
