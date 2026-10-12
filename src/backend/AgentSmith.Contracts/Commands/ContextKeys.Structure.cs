namespace AgentSmith.Contracts.Commands;

/// <summary>
/// 2026-10-06-8565d: context keys owned by the commit step's structure gate.
/// </summary>
public static partial class ContextKeys
{
    /// <summary>The units the commit gate found over their Limits, handed back to the master for
    /// its one correction. Removed when the gate passes.</summary>
    public const string StructureFindings = "StructureFindings";

    /// <summary>Set once a spec has had its structure correction, so a second finding is a
    /// verdict rather than a carousel.</summary>
    public const string StructureGateAttempted = "StructureGateAttempted";
}
