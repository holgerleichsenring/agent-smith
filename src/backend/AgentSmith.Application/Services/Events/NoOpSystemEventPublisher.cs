using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Events;

/// <summary>
/// CLI / test default. Lets every system-side producer call
/// <see cref="ISystemEventPublisher.PublishAsync"/> without a nullable guard
/// while Server's AddRedis swaps in the Redis variant.
/// </summary>
public sealed class NoOpSystemEventPublisher : ISystemEventPublisher
{
    public Task PublishAsync(SystemEvent systemEvent, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
