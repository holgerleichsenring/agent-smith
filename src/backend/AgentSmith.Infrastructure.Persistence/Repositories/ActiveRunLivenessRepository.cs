using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// The liveness SCANS over the lease table — who is still alive, and whose
/// heartbeat stopped. Separate from <see cref="ActiveRunRepository"/> (which owns
/// claim / attach / release for ONE ticket) because these two read the whole table
/// on behalf of the reapers, and they share the reason their filtering is
/// client-side.
/// </summary>
public sealed class ActiveRunLivenessRepository(IUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<StaleLease>> FindStaleAsync(TimeSpan olderThan, CancellationToken ct)
    {
        var cutoff = timeProvider.GetUtcNow() - olderThan;
        // SQLite cannot translate a DateTimeOffset comparison; the active-lease
        // set is small (one row per in-flight ticket), so filter client-side.
        var rows = await unitOfWork.Set<ActiveRun>()
            .Select(a => new { a.Project, a.TicketId, a.RunId, a.JobId, a.HeartbeatAt })
            .ToListAsync(ct);
        return rows
            .Where(r => r.HeartbeatAt < cutoff)
            .Select(r => new StaleLease(r.Project, new TicketId(r.TicketId), r.RunId, r.JobId, r.HeartbeatAt))
            .ToList();
    }

    public async Task<IReadOnlyCollection<string>> GetActiveRunIdsAsync(TimeSpan freshFor, CancellationToken ct)
    {
        var cutoff = timeProvider.GetUtcNow() - freshFor;
        // Same client-side filtering, same reason as FindStaleAsync.
        var rows = await unitOfWork.Set<ActiveRun>().AsNoTracking()
            .Select(a => new { a.RunId, a.HeartbeatAt })
            .ToListAsync(ct);
        return rows
            .Where(r => r.HeartbeatAt >= cutoff && !string.IsNullOrEmpty(r.RunId))
            .Select(r => r.RunId!)
            .ToList();
    }
}
