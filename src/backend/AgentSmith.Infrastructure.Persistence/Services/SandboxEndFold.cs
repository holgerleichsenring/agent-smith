using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-13-9a59i: the run's end names its sandboxes' release. The coordinator disposes before the
/// RunFinished is published, so a row still 'released' was released by this end (park, requeue or end)
/// and a row with no reason never saw its close — 'unobserved', DisposedAt left null.
/// <para>
/// Bounded by the RunFinished's own position: only rows created below it are touched, so a replayed park
/// never relabels a later launch's rows. A position-less RunFinished is the reconciler's terminal one —
/// nothing follows it, so every positioned row is folded. Rows without a position are never folded.
/// </para>
/// </summary>
public sealed class SandboxEndFold
{
    public async Task FoldAsync(IUnitOfWork uow, RunFinishedEvent e, long? eventSeq, CancellationToken ct)
    {
        if (eventSeq is null && RunStatuses.IsWaiting(e.Status)) return;
        var rows = uow.Set<RunSandbox>().Where(s => s.RunId == e.RunId && s.EventSeq != null
            && (s.EndReason == null || s.EndReason == SandboxEndReasons.Released));
        if (eventSeq is { } bound) rows = rows.Where(s => s.EventSeq < bound);
        var open = await rows.ToListAsync(ct);
        if (open.Count == 0) return;
        var released = SandboxEndRule.OfRelease(e.Status);
        foreach (var box in open)
            box.EndReason = box.EndReason == SandboxEndReasons.Released ? released : SandboxEndReasons.Unobserved;
        await uow.SaveChangesAsync(ct);
    }
}
