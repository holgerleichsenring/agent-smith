namespace AgentSmith.Contracts.Pipeline;

/// <summary>
/// A pipeline preset's declared data-flow edges. Co-located with the preset
/// definition so adding/removing a step forces a coordinated edge update.
/// Resolved at runtime by IStepDataFlowResolver via the active preset name.
/// </summary>
public interface IStepDataFlow
{
    /// <summary>Name of the preset this declaration belongs to (e.g. "code").</summary>
    string PresetName { get; }

    /// <summary>The directed edges between spec steps.</summary>
    IReadOnlyList<StepDataFlowEdge> Edges { get; }
}
