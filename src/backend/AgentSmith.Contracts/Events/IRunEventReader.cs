namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0177: read-side of the per-run event stream. Implementations:
/// in-process query for sync paths (tests + no-redis CLI), XRANGE-style
/// scan for the Redis transport. The <c>read_sub_agent_observations</c>
/// tool host consumes this to project the master's view of a child's
/// trace without forcing a summary.
/// </summary>
public interface IRunEventReader
{
    Task<IReadOnlyList<RunEvent>> ReadAsync(string runId, CancellationToken cancellationToken);
}
