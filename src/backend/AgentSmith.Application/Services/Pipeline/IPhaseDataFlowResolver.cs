using AgentSmith.Contracts.Pipeline;

namespace AgentSmith.Application.Services.Pipeline;

/// <summary>
/// Looks up the <see cref="IPhaseDataFlow"/> for a given pipeline preset name.
/// Returns null when no declaration is registered — PipelineExecutor falls back
/// to ungated behaviour for that run.
/// </summary>
public interface IPhaseDataFlowResolver
{
    IPhaseDataFlow? Resolve(string presetName);
}
