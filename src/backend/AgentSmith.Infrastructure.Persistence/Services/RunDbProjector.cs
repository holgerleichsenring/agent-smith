using System.Collections.Concurrent;
using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// Server-side single writer: projects every RunEvent the broadcaster drains
/// from the run stream into the relational store. Typed run facts go through
/// <see cref="RunEventApplier"/>; the raw event trail is BATCHED (flushed per N
/// events or on RunFinished) so the per-event payload writes don't dominate. The
/// spawned job never touches the DB — only this server-side projector does.
/// </summary>
public sealed class RunDbProjector(
    IServiceScopeFactory scopeFactory,
    RunEventApplier applier,
    TimeProvider timeProvider)
{
    private const int FlushThreshold = 25;
    // p0376: a partial buffer is drained once its oldest pending event has waited
    // this long, so the UI trail surfaces within ~a second instead of staying dark
    // until 25 events accumulate. RunTrailFlusherHostedService ticks FlushStaleAsync.
    private static readonly TimeSpan MaxBufferAge = TimeSpan.FromMilliseconds(750);
    private readonly ConcurrentDictionary<string, RunTrailBuffer> _buffers = new();

    public async Task ProjectAsync(AgentSmith.Contracts.Events.RunEvent runEvent, CancellationToken cancellationToken)
    {
        // The projector is a singleton consuming the run stream — NOT a web
        // request — so it opens a scope per event and applies the typed entity
        // through the scoped unit of work.
        using (var scope = scopeFactory.CreateScope())
            await applier.ApplyAsync(Uow(scope), runEvent, cancellationToken);

        var buffer = _buffers.GetOrAdd(runEvent.RunId, _ => new RunTrailBuffer());
        var toFlush = buffer.Add(runEvent, FlushThreshold, timeProvider.GetUtcNow());
        if (toFlush is not null) await FlushAsync(runEvent.RunId, toFlush, cancellationToken);
        if (runEvent.Type == EventType.RunFinished) _buffers.TryRemove(runEvent.RunId, out _);
    }

    /// <summary>
    /// p0376: flush every run's partial trail buffer whose oldest pending event has
    /// aged past <see cref="MaxBufferAge"/>. Called on a short timer by
    /// RunTrailFlusherHostedService so a sparse or paused run's trail does not sit
    /// unwritten. Concurrency-safe against <see cref="ProjectAsync"/>: the buffer
    /// hands each event to exactly one drain, so no seq is written twice.
    /// </summary>
    public async Task FlushStaleAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var (runId, buffer) in _buffers)
        {
            var toFlush = buffer.DrainIfOlderThan(MaxBufferAge, now);
            if (toFlush is not null) await FlushAsync(runId, toFlush, cancellationToken);
        }
    }

    /// <summary>
    /// p0423: drain every buffer regardless of age or threshold. A CLI one-shot has no
    /// flusher hosted service and simply exits, so without a final drain the last
    /// events of a run — the ones that say how it ended — never reach the store.
    /// </summary>
    public async Task FlushAllAsync(CancellationToken cancellationToken)
    {
        foreach (var (runId, buffer) in _buffers)
        {
            var toFlush = buffer.DrainIfOlderThan(TimeSpan.Zero, timeProvider.GetUtcNow());
            if (toFlush is not null) await FlushAsync(runId, toFlush, cancellationToken);
        }
    }

    private async Task FlushAsync(
        string runId, IReadOnlyList<(long Seq, AgentSmith.Contracts.Events.RunEvent Event)> events, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var uow = Uow(scope);
        foreach (var (seq, ev) in events) uow.Add(RunTrailRowMapper.Map(runId, seq, ev));
        await uow.SaveChangesAsync(ct);
    }

    private static IUnitOfWork Uow(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
}
