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
}
