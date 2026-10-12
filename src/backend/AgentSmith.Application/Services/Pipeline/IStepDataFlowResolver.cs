using AgentSmith.Contracts.Pipeline;

namespace AgentSmith.Application.Services.Pipeline;

/// <summary>
/// Looks up the <see cref="IStepDataFlow"/> for a given pipeline preset name.
/// Returns null when no declaration is registered — PipelineExecutor falls back
/// to ungated behaviour for that run.
/// </summary>
public interface IStepDataFlowResolver
{
    IStepDataFlow? Resolve(string presetName);
}
