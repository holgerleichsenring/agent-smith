using System.Collections.Concurrent;
using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>Synchronous event → DB projection (the fast tier has no Redis;
/// production routes the same events through RunDbProjector). Shared by the
/// p0327 durable-dialogue and p0328 expectation tests.
///
/// <para>p0388c: writes the RAW TRAIL ROW as well as the typed facts, through the
/// same <see cref="RunTrailRowMapper"/> production uses — that row is where a
/// trail-only event's step attribution (SubAgentSpawned, SandboxCommand) lands.
/// Unbuffered on purpose: RunDbProjector batches and relies on a hosted flusher
/// the harness never starts, so buffering here would make assertions depend on
/// wall-clock timing. Seq is assigned per run in publish order, exactly the
/// ordering the projector's buffer produces.</para>
/// </summary>
public sealed class ProjectingEventPublisher(IServiceScopeFactory scopeFactory) : IEventPublisher
{
    private readonly RunEventApplier _applier = new(new(), new(), new());
    private readonly ConcurrentDictionary<string, long> _seqByRun = new();

    public async Task PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await _applier.ApplyAsync(uow, runEvent, cancellationToken);
        uow.Add(RunTrailRowMapper.Map(runEvent.RunId, NextSeq(runEvent.RunId), runEvent));
        await uow.SaveChangesAsync(cancellationToken);
    }

    private long NextSeq(string runId) =>
        _seqByRun.AddOrUpdate(runId, 0L, (_, previous) => previous + 1);
}
