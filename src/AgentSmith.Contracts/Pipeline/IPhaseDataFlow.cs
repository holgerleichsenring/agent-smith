namespace AgentSmith.Contracts.Pipeline;

/// <summary>
/// A pipeline preset's declared data-flow edges. Co-located with the preset
/// definition so adding/removing a step forces a coordinated edge update.
/// Resolved at runtime by IPhaseDataFlowResolver via the active preset name.
/// </summary>
public interface IPhaseDataFlow
{
    /// <summary>Name of the preset this declaration belongs to (e.g. "fix-bug").</summary>
    string PresetName { get; }

    /// <summary>The directed edges between phase steps.</summary>
    IReadOnlyList<PhaseDataFlowEdge> Edges { get; }
}
