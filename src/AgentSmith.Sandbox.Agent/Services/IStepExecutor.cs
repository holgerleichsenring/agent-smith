using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Sandbox.Agent.Services;

internal interface IStepExecutor
{
    Task<StepResult> ExecuteAsync(
        Step step,
        Func<IReadOnlyList<StepEvent>, Task> onEvents,
        CancellationToken cancellationToken);
}
