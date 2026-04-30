using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Lifecycle;

/// <summary>
/// Default coordinator for compositions that do not need cross-process lifecycle
/// tracking (CLI one-shot runs). Begin returns a scope whose Dispose is a no-op.
/// </summary>
public sealed class NoOpPipelineLifecycleCoordinator : IPipelineLifecycleCoordinator
{
    public Task<IAsyncPipelineLifecycle> BeginAsync(
        ProjectConfig projectConfig, PipelineContext context, CancellationToken cancellationToken)
        => Task.FromResult<IAsyncPipelineLifecycle>(NoOpScope.Instance);

    private sealed class NoOpScope : IAsyncPipelineLifecycle
    {
        public static NoOpScope Instance { get; } = new();
        public void MarkFailed() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
