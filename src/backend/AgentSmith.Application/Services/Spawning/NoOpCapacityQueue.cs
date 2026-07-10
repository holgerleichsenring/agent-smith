using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Spawning;

/// <summary>
/// p0320c: the DB-free binding (CLI single-binary / in-process compositions):
/// no persistent queue exists, so enqueue records nothing and the head is always
/// empty — a capacity-deferred spawn stays the stateless defer-and-retry it was
/// before this phase. Mirrors <see cref="Claim.NoOpActiveRunLease"/>.
/// </summary>
public sealed class NoOpCapacityQueue : ICapacityQueue
{
    public Task<string> EnqueueAsync(CapacityQueueCandidate candidate, CancellationToken cancellationToken)
        => Task.FromResult(candidate.CandidateRunId);

    public Task<CapacityQueueEntry?> PeekHeadAsync(CancellationToken cancellationToken)
        => Task.FromResult<CapacityQueueEntry?>(null);

    public Task RemoveAsync(string project, string ticketId, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task<int> CountAsync(CancellationToken cancellationToken)
        => Task.FromResult(0);

    public Task<IReadOnlyDictionary<string, int>> GetPositionsByRunIdAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>());
}
