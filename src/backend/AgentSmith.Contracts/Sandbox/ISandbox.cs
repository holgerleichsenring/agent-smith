using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Contracts.Sandbox;

public interface ISandbox : IAsyncDisposable
{
    string JobId { get; }

    Task<StepResult> RunStepAsync(
        Step step,
        IProgress<StepEvent>? progress,
        CancellationToken cancellationToken);
}
