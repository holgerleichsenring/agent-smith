using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// 2026-10-13-9a59j: a run's launches in launch order, each with its model time and, when it parked, who it
/// waited on. Model time is CALL time — the summed duration of the calls whose trail position lies inside the
/// segment — so concurrent sub-agent calls each count in full and it can exceed the segment's wall clock. A
/// call inside no segment (no position, or made before the run's segments were recorded) is counted once
/// for the run.
/// </summary>
public sealed class RunSegmentsReader(RunParkReader parks)
{
    public async Task<RunSegmentsRead> ReadAsync(IUnitOfWork uow, string runId, CancellationToken ct)
    {
        var segments = await uow.Set<RunSegment>().AsNoTracking()
            .Where(s => s.RunId == runId && s.EventSeq != null).OrderBy(s => s.EventSeq).ToListAsync(ct);
        var calls = await uow.Set<RunLlmCall>().AsNoTracking().Where(c => c.RunId == runId)
            .Select(c => new CallTime(c.EventSeq, c.DurationMs)).ToListAsync(ct);
        var parked = await parks.ReadAsync(uow, runId, segments, ct);
        var views = new List<RunComputeSegmentView>(segments.Count);
        var attributed = 0;
        for (var i = 0; i < segments.Count; i++)
        {
            var inside = calls.Where(c => Inside(segments, i, c.EventSeq)).ToList();
            attributed += inside.Count;
            var s = segments[i];
            views.Add(new RunComputeSegmentView(
                s.StartedAt, s.EndedAt, s.EndStatus, inside.Sum(c => c.DurationMs), parked[i]));
        }
        return new RunSegmentsRead(views, calls.Count - attributed);
    }

    /// <summary>
    /// A position belongs to segment i from its RunStarted up to its closing RunFinished; an open end (the live
    /// launch, the reconciler's position-less close) reaches the next launch's start, or has no bound.
    /// </summary>
    internal static bool Inside(IReadOnlyList<RunSegment> segments, int i, long? position)
    {
        if (position is not { } at || at < segments[i].EventSeq) return false;
        var end = segments[i].EndSeq ?? (i + 1 < segments.Count ? segments[i + 1].EventSeq : null);
        return end is null || at < end;
    }

    private sealed record CallTime(long? EventSeq, long DurationMs);
}

/// <summary>2026-10-13-9a59j: a run's segments and the calls that fall in none of them.</summary>
public sealed record RunSegmentsRead(IReadOnlyList<RunComputeSegmentView> Segments, int UnattributedCalls);

/// <summary>
/// 2026-10-13-9a59j: one launch. EndStatus is the closing RunFinished's status (null while it runs);
/// 'queued' is a capacity wait and never carries a park. ModelMs is call time, never a share of the
/// segment's wall clock.
/// </summary>
public sealed record RunComputeSegmentView(
    DateTimeOffset StartedAt, DateTimeOffset? EndedAt, string? EndStatus, long ModelMs, RunComputeParkView? Park);
