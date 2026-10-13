namespace AgentSmith.Contracts.Models.Structure;

/// <summary>
/// 2026-10-12-fbbda: how far past its limit a unit is. A divergence is inside the tolerance band
/// and is named in its repository's pull request; an excess is past the band and is what the
/// commit gate sends back and the baseline records.
/// </summary>
public enum StructureSeverity
{
    /// <summary>Past the limit, at or below floor(limit * (100 + tolerance) / 100).</summary>
    Divergence,

    /// <summary>Past the tolerance band.</summary>
    Excess,
}
