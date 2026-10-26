using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// Server-side single writer: projects every RunEvent the broadcaster drains
/// from the run stream into the relational store. Typed run facts go through
/// <see cref="RunEventApplier"/>; the raw event trail is BATCHED (flushed per N
/// events or on RunFinished) so the per-event payload writes don't dominate. The
/// pipeline never writes these rows itself — only this server-side projector does.
/// </summary>
public sealed class RunDbProjector(
    IServiceScopeFactory scopeFactory,
    RunEventApplier applier,
    RunTrailBuffers buffers,
    RunTrailWriter trail,
    TimeProvider timeProvider)
{
    private const int FlushThreshold = 25;
    // p0376: a partial buffer is drained once its oldest pending event has waited
    // this long, so the UI trail surfaces within ~a second instead of staying dark
    // until 25 events accumulate. RunTrailFlusherHostedService ticks FlushStaleAsync.
    private static readonly TimeSpan MaxBufferAge = TimeSpan.FromMilliseconds(750);

    public Task ProjectAsync(AgentSmith.Contracts.Events.RunEvent runEvent, CancellationToken cancellationToken) =>
        ProjectAsync(runEvent, null, cancellationToken);

    /// <summary>
    /// 2026-10-26-0f19a: <paramref name="position"/> is the event's place in its run stream
    /// (<see cref="StreamTrailPosition"/>), the same on every replica; null — the CLI's direct
    /// projection, which has no stream — mints one.
    /// </summary>
    public async Task ProjectAsync(
        AgentSmith.Contracts.Events.RunEvent runEvent, long? position, CancellationToken cancellationToken)
    {
        // 2026-08-25-61f1: the position is minted BEFORE the typed facts are applied — it is the
        // identity every row the event produces is written under. A sandbox result writes its own
        // trail row with its fold (2026-10-26-0f19a), so it never waits in the buffer.
        var buffer = await buffers.ForAsync(runEvent.RunId, cancellationToken);
        var added = runEvent is SandboxResultEvent
            ? new TrailAddition(buffer.Reserve(position), null)
            : buffer.Add(runEvent, position, FlushThreshold, timeProvider.GetUtcNow());
        // 2026-10-26-0f19a: a projection that lost a unique race to another writer is re-run once, in a
        // fresh scope, and finds that writer's rows.
        try { await ApplyAsync(runEvent, added.Seq, cancellationToken); }
        catch (DbUpdateException) { await ApplyAsync(runEvent, added.Seq, cancellationToken); }
        if (added.ToFlush is not null) await trail.WriteAsync(runEvent.RunId, added.ToFlush, cancellationToken);
        // 2026-08-24-ca23: only a real ending releases the buffer. A waiting status relaunches
        // onto this same run id, and dropping the buffer restarts its sequence at zero.
        if (runEvent.Type == EventType.RunFinished
            && runEvent is RunFinishedEvent finished && !RunStatuses.IsWaiting(finished.Status))
            buffers.Release(runEvent.RunId);
    }

    // The projector is a singleton consuming the run stream — NOT a web request — so it opens a
    // scope per event and applies the typed entity through the scoped unit of work.
    private async Task ApplyAsync(AgentSmith.Contracts.Events.RunEvent runEvent, long seq, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        await applier.ApplyAsync(Uow(scope), runEvent, seq, ct);
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
            if (toFlush is not null) await trail.WriteAsync(runId, toFlush, cancellationToken);
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
            if (toFlush is not null) await trail.WriteAsync(runId, toFlush, cancellationToken);
        }
    }

    private static IUnitOfWork Uow(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
}
