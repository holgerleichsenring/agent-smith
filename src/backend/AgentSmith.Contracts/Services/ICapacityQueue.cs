using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0320c: the persistent FIFO capacity queue. One entry per (project, ticket)
/// waiting for sandbox capacity, ordered strictly by arrival — a smaller run
/// never overtakes the head. The DB-backed implementation also owns the single
/// visible "queued" Run row per entry (created on first enqueue, reused on every
/// retry); the DB-free composition binds a no-op so CLI/in-process runs keep
/// today's stateless defer-and-retry behavior.
/// </summary>
public interface ICapacityQueue
{
    /// <summary>
    /// Upserts the entry for (Project, TicketId). First enqueue stores the
    /// candidate's reserved run id and creates its queued Run row; a retry keeps
    /// the existing order and reserved id (only the waiting reason refreshes).
    /// Returns the entry's effective reserved run id.
    /// </summary>
    Task<string> EnqueueAsync(CapacityQueueCandidate candidate, CancellationToken cancellationToken);

    /// <summary>The head of the queue (lowest arrival order), or null when empty.</summary>
    Task<CapacityQueueEntry?> PeekHeadAsync(CancellationToken cancellationToken);

    Task RemoveAsync(string project, string ticketId, CancellationToken cancellationToken);

    Task<int> CountAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 1-based FIFO positions keyed by reserved run id, computed from the live
    /// queue order at read time (positions are never persisted — the head moves).
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> GetPositionsByRunIdAsync(CancellationToken cancellationToken);
}
