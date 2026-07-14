using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>Synchronous event → DB projection (the fast tier has no Redis;
/// production routes the same events through RunDbProjector's applier).
/// Shared by the p0327 durable-dialogue and p0328 expectation tests.</summary>
public sealed class ProjectingEventPublisher(IServiceScopeFactory scopeFactory) : IEventPublisher
{
    private readonly RunEventApplier _applier = new();

    public async Task PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var uow = scope.ServiceProvider
            .GetRequiredService<AgentSmith.Infrastructure.Persistence.Contracts.IUnitOfWork>();
        await _applier.ApplyAsync(uow, runEvent, cancellationToken);
    }
}
