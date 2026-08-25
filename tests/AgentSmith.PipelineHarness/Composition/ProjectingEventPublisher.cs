using System.Collections.Concurrent;
using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.EntityFrameworkCore;
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
    private readonly RunEventApplier _applier = new(
        checkpoints: new(), expectations: new(), queuedRuns: new(), sandboxes: new(new(), new()),
        steps: new(new()), pullRequests: new(), classification: new(), finalization: new(new()),
        phases: new(), llmCalls: new(new(), new(), new()), decisions: new(new()));

    private readonly ConcurrentDictionary<string, long> _seqByRun = new();

    public async Task PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        // 2026-08-25-61f1: the position is minted BEFORE the typed facts are applied, because
        // every row the event produces is written under it — exactly as production does it.
        var seq = await NextSeqAsync(uow, runEvent.RunId, cancellationToken);
        await _applier.ApplyAsync(uow, runEvent, seq, cancellationToken);
        uow.Add(RunTrailRowMapper.Map(runEvent.RunId, seq, runEvent));
        await uow.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 2026-08-25-61f1: seeded from what the store already holds, the way RunTrailBuffers is.
    /// A restart preset builds a second publisher over the SAME store, and a counter that
    /// starts at zero there re-mints positions the first leg already wrote — the very
    /// collision the trail's uniqueness now refuses.
    /// </summary>
    private async Task<long> NextSeqAsync(IUnitOfWork uow, string runId, CancellationToken ct)
    {
        if (!_seqByRun.ContainsKey(runId))
            _seqByRun[runId] = await uow.Set<Infrastructure.Persistence.Entities.RunEvent>()
                .AsNoTracking().Where(e => e.RunId == runId).MaxAsync(e => (long?)e.Seq, ct) ?? -1L;
        return _seqByRun.AddOrUpdate(runId, 0L, (_, previous) => previous + 1);
    }
}
