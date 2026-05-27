using AgentSmith.Contracts.Events;

namespace AgentSmith.Tests.Events;

/// <summary>
/// p0173a: simulates a silent producer for the system-channel completeness
/// theory. Identical shape to <see cref="AgentSmith.Application.Services.Events.NoOpSystemEventPublisher"/>
/// but lives in the test project so the theory rows can inject it as the
/// "dropped" publisher per row.
/// </summary>
public sealed class DroppingSystemEventPublisher : ISystemEventPublisher
{
    public Task PublishAsync(SystemEvent systemEvent, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
