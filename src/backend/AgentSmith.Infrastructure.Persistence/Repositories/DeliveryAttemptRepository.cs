using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-25-0099b: the ticket attempts of a window in the caller's projects, newest first and bounded by a
/// stated ceiling; each with the channel its RunAttempt row recorded, through a left join, because a run that
/// died before it fetched its ticket has no such row.
/// <para>2026-10-26-7000c: the window's spec derivations in the same shape, scoped and bounded alike, with no channel.</para>
/// </summary>
public sealed class DeliveryAttemptRepository(IUnitOfWork unitOfWork)
{
    public async Task<DeliveryAttemptRead> ReadAsync(
        ProjectScope scope, string fromId, string toId, int ceiling, CancellationToken ct)
    {
        var rows = await (
                from run in unitOfWork.Set<Run>().AsNoTracking().InWindow(scope, fromId, toId)
                join attempt in unitOfWork.Set<RunAttempt>().AsNoTracking() on run.Id equals attempt.RunId into causes
                from attempt in causes.DefaultIfEmpty()
                orderby run.Id descending
                select new DeliveryAttemptRow(
                    run.Id, run.Project, run.TicketId, run.TicketTitle, run.Status, run.EndKind, run.StartedAt,
                    run.FinishedAt, run.DurationSeconds, run.CostTotalUsd, attempt == null ? null : attempt.CauseChannel))
            .Take(ceiling + 1)
            .ToListAsync(ct);
        return Bounded(rows, ceiling);
    }

    public async Task<DeliveryAttemptRead> DerivationsAsync(
        ProjectScope scope, string fromId, string toId, int ceiling, CancellationToken ct)
    {
        var rows = await unitOfWork.Set<Run>().AsNoTracking().DerivationsInWindow(scope, fromId, toId)
            .OrderByDescending(run => run.Id)
            .Select(run => new DeliveryAttemptRow(
                run.Id, run.Project, run.TicketId, run.TicketTitle, run.Status, run.EndKind, run.StartedAt,
                run.FinishedAt, run.DurationSeconds, run.CostTotalUsd, null))
            .Take(ceiling + 1)
            .ToListAsync(ct);
        return Bounded(rows, ceiling);
    }

    private static DeliveryAttemptRead Bounded(List<DeliveryAttemptRow> rows, int ceiling) =>
        new([.. rows.Take(ceiling)], rows.Count > ceiling);
}
