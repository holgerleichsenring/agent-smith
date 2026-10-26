using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0378: stream-authoritative terminal repair, invoked from the broadcaster's
/// cold-start rehydrate. A cold start anchors drain cursors at the stream TAIL
/// (p0258 anti-replay), so a RunFinished the previous process never persisted
/// would otherwise be skipped forever — leaving the row 'running' (run fb2d).
/// Waiting statuses (queued / waiting_for_input) are not terminal and are left
/// to the normal launch path.
/// </summary>
public sealed class RunTerminalReconciler(
    IServiceScopeFactory scopeFactory,
    RunEventApplier applier,
    ILogger<RunTerminalReconciler> logger) : IRunTerminalReconciler
{
    public async Task ReconcileAsync(RunFinishedEvent terminal, long position, CancellationToken cancellationToken)
    {
        if (RunStatuses.IsWaiting(terminal.Status)) return;
        using var scope = scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var run = await uow.Set<Run>().FirstOrDefaultAsync(r => r.Id == terminal.RunId, cancellationToken);
        if (run is null) return;
        if (run.FinishedAt is null) await FinalizeRowAsync(uow, terminal, cancellationToken);
        await AppendTrailRowIfMissingAsync(uow, terminal, position, cancellationToken);
    }

    private async Task FinalizeRowAsync(IUnitOfWork uow, RunFinishedEvent terminal, CancellationToken ct)
    {
        logger.LogWarning(
            "Run {RunId}: stream carries terminal RunFinished ({Status}) but the row is not terminal — reconciling",
            terminal.RunId, terminal.Status);
        await applier.ApplyAsync(uow, terminal, ct);
    }

    // 2026-10-26-0f19a: at the terminal entry's own position, so a live replica that projects the same
    // entry finds this row instead of writing a second RunFinished beside it — and loses to it in a race.
    private async Task AppendTrailRowIfMissingAsync(
        IUnitOfWork uow, RunFinishedEvent terminal, long position, CancellationToken ct)
    {
        var rows = uow.Set<Entities.RunEvent>().Where(e => e.RunId == terminal.RunId);
        if (await rows.AnyAsync(e => e.Type == nameof(EventType.RunFinished) || e.Seq == position, ct)) return;
        logger.LogWarning(
            "Run {RunId}: RunFinished trail row missing — appending from the stream's terminal event",
            terminal.RunId);
        uow.Add(RunTrailRowMapper.Map(terminal.RunId, position, terminal));
        try { await uow.SaveChangesAsync(ct); }
        catch (DbUpdateException) { logger.LogInformation("Run {RunId}: another writer appended it first", terminal.RunId); }
    }
}
