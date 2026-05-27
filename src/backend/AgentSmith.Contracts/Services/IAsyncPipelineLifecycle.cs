namespace AgentSmith.Contracts.Services;

/// <summary>
/// Lifecycle scope returned by <see cref="IPipelineLifecycleCoordinator.BeginAsync"/>.
/// Disposed asynchronously at the end of a pipeline run; <see cref="MarkFailed"/>
/// signals the executor's final outcome so the dispose can transition to the
/// appropriate terminal status.
/// </summary>
public interface IAsyncPipelineLifecycle : IAsyncDisposable
{
    void MarkFailed();
}
