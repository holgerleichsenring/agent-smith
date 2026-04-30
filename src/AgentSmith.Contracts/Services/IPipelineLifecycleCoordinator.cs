using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Wraps a pipeline run with whatever cross-process lifecycle plumbing the
/// composition needs. Server-side: ticket status transitions + Redis heartbeat.
/// CLI side: a no-op (no ticket, no multi-worker coordination).
/// </summary>
public interface IPipelineLifecycleCoordinator
{
    Task<IAsyncPipelineLifecycle> BeginAsync(
        ProjectConfig projectConfig,
        PipelineContext context,
        CancellationToken cancellationToken);
}
