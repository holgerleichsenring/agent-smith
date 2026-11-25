using AgentSmith.Contracts.Models.Structure;

namespace AgentSmith.Application.Services.Structure;

/// <summary>One context's judging terms as they stood at the branch's base. 2026-10-13-2d0c: the
/// baseline is keyed by path+key+metric, so each finding is one lookup. 2026-11-24-8756:
/// <paramref name="CountsBreaks"/> is false for a baseline written before units counted the breaks a
/// formatter would add; the gate then also tolerates a unit no larger than at the base
/// (<see cref="StructureBaseCounts"/>).</summary>
public sealed record StructureContextRules(
    string Workdir, StructureLimitSet Limits, IReadOnlyDictionary<(string, string, string), int> Tolerated,
    bool CountsBreaks = true)
{
    /// <summary>Whether the baseline tolerates a finding: the same unit, not grown past its entry.</summary>
    public bool Tolerates(StructureFinding finding) =>
        Tolerated.TryGetValue((finding.Path, finding.Key, finding.Metric), out var value) && finding.Value <= value;

    /// <summary>2026-11-24-8756: whether the physical-line rule of an uncounted baseline passed the
    /// unit at the base — inside the tolerance band, or within its entry.</summary>
    public bool PassedAtBase(StructureFinding finding, int physicalAtBase) =>
        physicalAtBase <= Limits.BandEnd(finding.Limit)
        || (Tolerated.TryGetValue((finding.Path, finding.Key, finding.Metric), out var value) && physicalAtBase <= value);
}
