using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Sandbox.Agent.Services;

internal interface IProcessRunner
{
    Task<ProcessOutcome> RunAsync(
        Step step,
        Action<StepEventKind, string> onLine,
        CancellationToken cancellationToken);
}
