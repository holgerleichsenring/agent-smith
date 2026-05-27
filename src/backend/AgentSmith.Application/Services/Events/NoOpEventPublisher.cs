using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Events;

/// <summary>
/// CLI / test default. Lets every producer call <see cref="IEventPublisher.PublishAsync"/>
/// without a nullable guard while Server's AddRedis swaps in the Redis variant.
/// </summary>
public sealed class NoOpEventPublisher : IEventPublisher
{
    public Task PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
