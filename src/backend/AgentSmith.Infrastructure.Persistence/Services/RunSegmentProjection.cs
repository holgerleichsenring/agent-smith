using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using RunEvent = AgentSmith.Contracts.Events.RunEvent;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-13-9a59j: the segment family — one <see cref="RunSegment"/> per launch. A RunStarted opens one at its
/// trail position (a replay holds it and adds none); a RunFinished closes the latest segment opened below its
/// own position, and only while that segment is open, so a replayed park never touches a later launch. The
/// terminal reconciler's position-less RunFinished closes the run's latest open segment with no end position.
/// A RunStarted without a position is never segmented: nothing could bound it.
/// </summary>
public sealed class RunSegmentProjection(ProjectedEventRecords records) : IRunEventFamily
{
    public IReadOnlyCollection<Type> Events { get; } = [typeof(RunStartedEvent), typeof(RunFinishedEvent)];

    public Task ApplyAsync(IUnitOfWork uow, RunEvent runEvent, long? eventSeq, CancellationToken ct) => runEvent switch
    {
        RunStartedEvent e when eventSeq is not null => OpenAsync(uow, e, eventSeq.Value, ct),
        RunFinishedEvent e => CloseAsync(uow, e, eventSeq, ct),
        _ => Task.CompletedTask,
    };

    private async Task OpenAsync(IUnitOfWork uow, RunStartedEvent e, long eventSeq, CancellationToken ct)
    {
        if (await records.HoldsAsync<RunSegment>(uow, e.RunId, eventSeq, ct)) return;
        uow.Add(new RunSegment { RunId = e.RunId, EventSeq = eventSeq, StartedAt = e.StartedAt });
        await uow.SaveChangesAsync(ct);
    }

    private static async Task CloseAsync(IUnitOfWork uow, RunFinishedEvent e, long? eventSeq, CancellationToken ct)
    {
        if (eventSeq is null && RunStatuses.IsWaiting(e.Status)) return;
        var rows = uow.Set<RunSegment>().Where(s => s.RunId == e.RunId);
        if (eventSeq is { } bound) rows = rows.Where(s => s.EventSeq < bound);
        var segment = await rows.OrderByDescending(s => s.EventSeq).FirstOrDefaultAsync(ct);
        if (segment is null || segment.EndedAt is not null) return;
        segment.EndSeq = eventSeq;
        segment.EndedAt = e.Timestamp;
        segment.EndStatus = e.Status;
        await uow.SaveChangesAsync(ct);
    }
}
