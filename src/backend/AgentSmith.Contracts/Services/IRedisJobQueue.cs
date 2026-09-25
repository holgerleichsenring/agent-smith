using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Durable handoff of PipelineRequests between receiver (webhook/poller) and worker (consumer).
/// Redis-backed list with RPUSH on enqueue, BRPOP on consume.
/// Queue itself is ephemeral — 2026-09-25-b4d9: the taken-ticket record is what survives it,
/// and EnqueuedReconciler re-enqueues from that record rather than from a label on a board.
/// </summary>
public interface IRedisJobQueue
{
    Task EnqueueAsync(PipelineRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Blocks on BRPOP and yields items as they arrive. Co-operates with the CancellationToken
    /// via a short block timeout (re-blocks in a loop) so shutdown is bounded.
    /// </summary>
    IAsyncEnumerable<PipelineRequest> ConsumeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Current queue depth (LLEN). Used by the reconciler and observability.
    /// </summary>
    Task<long> LenAsync(CancellationToken cancellationToken);
}
