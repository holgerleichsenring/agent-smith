using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-08-25-a508: the runs currently stopped on a question. A park keeps FinishedAt null
/// and leaves the active set, so no reaper and no terminal path lists these — they are found
/// by their status alone, oldest first.
/// </summary>
public sealed class ParkedRunRepository(IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyList<ParkedRun>> ListAsync(CancellationToken cancellationToken)
    {
        return await unitOfWork.Set<Run>().AsNoTracking()
            .Where(r => r.FinishedAt == null
                && !r.CancelRequested
                && r.Status == RunStatuses.WaitingForInput)
            // The run id is the sortable ISO-8601 stamp, so this IS oldest first — and
            // SQLite cannot order by a DateTimeOffset column.
            .OrderBy(r => r.Id)
            .Select(r => new ParkedRun(r.Id, r.Project, r.TicketId))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 2026-10-16-86e6b: the parked code runs a newer code run of the same ticket has started past — it has a
    /// step, so it worked. A park whose answer is already on its way back (a resumed checkpoint, a queue entry
    /// reserved for it) is left to its relaunch.
    /// </summary>
    public async Task<IReadOnlyList<SupersededPark>> ListSupersededAsync(CancellationToken cancellationToken)
    {
        var runs = unitOfWork.Set<Run>().AsNoTracking();
        return await runs
            .Where(r => r.FinishedAt == null && !r.CancelRequested && r.Status == RunStatuses.WaitingForInput
                && r.Pipeline == PipelinePresets.CodeName && r.TicketId != ""
                && !unitOfWork.Set<RunCheckpoint>().Any(c => c.RunId == r.Id && c.ResumedAt != null)
                && !unitOfWork.Set<QueuedTicket>().Any(q => q.ReservedRunId == r.Id))
            .OrderBy(r => r.Id)
            .Select(r => new
            {
                r.Id, r.Project, r.TicketId,
                Newer = runs.Where(n => n.Project == r.Project && n.TicketId == r.TicketId
                        && n.Pipeline == PipelinePresets.CodeName && string.Compare(n.Id, r.Id) > 0
                        && unitOfWork.Set<RunStep>().Any(s => s.RunId == n.Id))
                    .OrderByDescending(n => n.Id).Select(n => n.Id).FirstOrDefault(),
            })
            .Where(p => p.Newer != null)
            .Select(p => new SupersededPark(p.Id, p.Project, p.TicketId, p.Newer!))
            .ToListAsync(cancellationToken);
    }
}
