using AgentSmith.Contracts.Events;
using RunEvent = AgentSmith.Contracts.Events.RunEvent;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-17-e061a: records every run event the composition publishes, then applies them through the real run-event
/// applier into the harness database — what the server's drain does — so a test reads the runs the dashboard's run
/// list reads.
/// </summary>
internal sealed class ActRunEvents : IEventPublisher
{
    private readonly List<RunEvent> _events = [];

    public IReadOnlyList<RunStartedEvent> Started { get { lock (_events) return [.. _events.OfType<RunStartedEvent>()]; } }

    public Task PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default)
    {
        lock (_events) _events.Add(runEvent);
        return Task.CompletedTask;
    }

    /// <summary>The finished runs the dashboard's run list shows, after every recorded event was applied.</summary>
    public async Task<IReadOnlyList<Run>> ListedRunsAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var applier = scope.ServiceProvider.GetRequiredService<RunEventApplier>();
        RunEvent[] events;
        lock (_events) events = [.. _events];
        for (var i = 0; i < events.Length; i++) await applier.ApplyAsync(uow, events[i], i + 1, default);
        await uow.SaveChangesAsync(default);
        return await new RunRepository(uow).GetRecentRunsAsync(ProjectScope.Everything, 50, default);
    }
}
