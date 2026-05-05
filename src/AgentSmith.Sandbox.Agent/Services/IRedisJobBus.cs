using AgentSmith.Sandbox.Agent.Models;

namespace AgentSmith.Sandbox.Agent.Services;

internal interface IRedisJobBus : IAsyncDisposable
{
    Task<Step?> WaitForStepAsync(string jobId, TimeSpan timeout, CancellationToken cancellationToken);

    void EnqueueEventsBatch(string jobId, IReadOnlyList<StepEvent> events);

    Task PushResultAsync(string jobId, StepResult result, CancellationToken cancellationToken);
}
