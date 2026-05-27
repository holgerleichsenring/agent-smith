namespace AgentSmith.Contracts.Events;

/// <summary>
/// Publishes a typed <see cref="SystemEvent"/> into the <c>system:events</c>
/// Redis Stream. Parallel to <see cref="IEventPublisher"/>; distinct
/// interface because system events have no runId (and the run-scoped
/// contract requires one), so a unified publisher would force every system-
/// side caller to handle a sentinel.
///
/// The Redis variant maintains MAXLEN + EXPIRE on every append. The NoOp
/// variant satisfies CLI / test paths that have no Redis without forcing
/// every call site to a nullable publisher.
/// </summary>
public interface ISystemEventPublisher
{
    Task PublishAsync(SystemEvent systemEvent, CancellationToken cancellationToken = default);
}
