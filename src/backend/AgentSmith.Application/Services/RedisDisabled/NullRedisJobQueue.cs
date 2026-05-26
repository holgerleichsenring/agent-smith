using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.RedisDisabled;

/// <summary>
/// Fallback IRedisJobQueue for Redis-less CLI runs. Manual pipelines (security-scan, fix
/// from CLI) never enqueue/consume; only the server-mode webhook/poller path does. Resolving
/// the dep succeeds; calling the methods throws with a clear actionable message.
/// </summary>
public sealed class NullRedisJobQueue : IRedisJobQueue
{
    public Task EnqueueAsync(PipelineRequest request, CancellationToken cancellationToken)
        => throw new RedisUnavailableException("Job-queue enqueue");

    public IAsyncEnumerable<PipelineRequest> ConsumeAsync(CancellationToken cancellationToken)
        => throw new RedisUnavailableException("Job-queue consume");

    public Task<long> LenAsync(CancellationToken cancellationToken) => Task.FromResult(0L);
}
