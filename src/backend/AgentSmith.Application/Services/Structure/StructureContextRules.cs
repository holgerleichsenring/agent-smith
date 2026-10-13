using AgentSmith.Contracts.Models.Structure;

namespace AgentSmith.Application.Services.Structure;

/// <summary>One context's judging terms as they stood at the branch's base. 2026-10-13-2d0c: the
/// baseline is keyed by path+key+metric, so each finding is one lookup.</summary>
public sealed record StructureContextRules(
    string Workdir, StructureLimitSet Limits, IReadOnlyDictionary<(string, string, string), int> Tolerated)
{
    /// <summary>Whether the baseline tolerates a finding: the same unit, not grown past its entry.</summary>
    public bool Tolerates(StructureFinding finding) =>
        Tolerated.TryGetValue((finding.Path, finding.Key, finding.Metric), out var value) && finding.Value <= value;
}
