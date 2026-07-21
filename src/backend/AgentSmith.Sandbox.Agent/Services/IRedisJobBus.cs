using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Sandbox.Agent.Services;

internal interface IRedisJobBus : IAsyncDisposable
{
    Task<Step?> WaitForStepAsync(string jobId, TimeSpan timeout, CancellationToken cancellationToken);

    void EnqueueEventsBatch(string jobId, IReadOnlyList<StepEvent> events);

    Task PushResultAsync(string jobId, StepResult result, CancellationToken cancellationToken);

    /// <summary>p0360b: is the given run id in the server's active-run set? Consulted at
    /// the idle limit so a sandbox whose run still lives keeps waiting instead of exiting.</summary>
    Task<bool> IsRunActiveAsync(string runId, CancellationToken cancellationToken);
}
