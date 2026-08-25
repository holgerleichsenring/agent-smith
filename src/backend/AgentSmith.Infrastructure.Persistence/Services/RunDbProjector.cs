using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using Microsoft.EntityFrameworkCore;
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
    RunTrailBuffers buffers,
    TimeProvider timeProvider)
{
    private const int FlushThreshold = 25;
    // p0376: a partial buffer is drained once its oldest pending event has waited
    // this long, so the UI trail surfaces within ~a second instead of staying dark
    // until 25 events accumulate. RunTrailFlusherHostedService ticks FlushStaleAsync.
    private static readonly TimeSpan MaxBufferAge = TimeSpan.FromMilliseconds(750);

    public async Task ProjectAsync(AgentSmith.Contracts.Events.RunEvent runEvent, CancellationToken cancellationToken)
    {
        // 2026-08-25-61f1: the buffer mints the event's trail position BEFORE the typed
        // facts are applied, because that position is the identity every row the event
        // produces is written under. Nothing is written yet — the batch still flushes
        // after the typed facts, exactly as before.
        var buffer = await buffers.ForAsync(runEvent.RunId, cancellationToken);
        var added = buffer.Add(runEvent, FlushThreshold, timeProvider.GetUtcNow());

        // The projector is a singleton consuming the run stream — NOT a web
        // request — so it opens a scope per event and applies the typed entity
        // through the scoped unit of work.
        using (var scope = scopeFactory.CreateScope())
            await applier.ApplyAsync(Uow(scope), runEvent, added.Seq, cancellationToken);

        if (added.ToFlush is not null) await FlushAsync(runEvent.RunId, added.ToFlush, cancellationToken);
        // 2026-08-24-ca23: only a real ending releases the buffer. A waiting status relaunches
        // onto this same run id, and dropping the buffer restarts its sequence at zero.
        if (runEvent.Type == EventType.RunFinished
            && runEvent is RunFinishedEvent finished && !RunStatuses.IsWaiting(finished.Status))
            buffers.Release(runEvent.RunId);
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
        foreach (var (runId, buffer) in buffers.All())
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
        foreach (var (runId, buffer) in buffers.All())
        {
            var toFlush = buffer.DrainIfOlderThan(TimeSpan.Zero, timeProvider.GetUtcNow());
            if (toFlush is not null) await FlushAsync(runId, toFlush, cancellationToken);
        }
    }

    /// <summary>
    /// 2026-08-25-61f1: insert-if-absent, per row. The store holds one trail row per
    /// (run, position) and the buffer drained this batch out of memory before the write —
    /// so a batch that THREW on a duplicate would be gone, unretried, and the drain loop
    /// would log it as transient and move on. Refusing must not mean losing: the positions
    /// the store already holds are dropped from the batch and the rest are written.
    /// </summary>
    private async Task FlushAsync(
        string runId, IReadOnlyList<(long Seq, AgentSmith.Contracts.Events.RunEvent Event)> events, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var uow = Uow(scope);
        var recorded = await RecordedSeqsAsync(uow, runId, [.. events.Select(e => e.Seq)], ct);
        foreach (var (seq, ev) in events.Where(e => !recorded.Contains(e.Seq)))
            uow.Add(RunTrailRowMapper.Map(runId, seq, ev));
        await uow.SaveChangesAsync(ct);
    }

    private static async Task<HashSet<long>> RecordedSeqsAsync(
        IUnitOfWork uow, string runId, IReadOnlyList<long> seqs, CancellationToken ct) =>
        [.. await uow.Set<Entities.RunEvent>().AsNoTracking()
            .Where(e => e.RunId == runId && seqs.Contains(e.Seq))
            .Select(e => e.Seq).ToListAsync(ct)];

    private static IUnitOfWork Uow(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
}
