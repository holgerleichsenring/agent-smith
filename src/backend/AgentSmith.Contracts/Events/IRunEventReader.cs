namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0177: read-side of the per-run event stream. Implementations:
/// in-process query for sync paths (tests + no-redis CLI), XRANGE-style
/// scan for the Redis transport, for the readers of a child's trace.
/// </summary>
public interface IRunEventReader
{
    Task<IReadOnlyList<RunEvent>> ReadAsync(string runId, CancellationToken cancellationToken);
}
