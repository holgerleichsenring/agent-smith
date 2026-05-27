namespace AgentSmith.Contracts.Events;

/// <summary>
/// Publishes a typed <see cref="RunEvent"/> into the per-run Redis Stream
/// (<c>run:{runId}:events</c>). The Redis variant maintains the two index
/// pointers (active set + recent list); the NoOp variant satisfies CLI /
/// test paths that have no Redis without forcing every call site to a
/// nullable publisher.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default);
}
