using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// Data access for the single-run lease over a SCOPED unit of work (no
/// IDbContextFactory). TryClaim is an INSERT the UNIQUE(Project,TicketId) index
/// rejects for a duplicate; the provider-native violation maps to AlreadyClaimed
/// via <see cref="IUniqueViolationTranslator"/>.
/// </summary>
public sealed class ActiveRunRepository(
    IUnitOfWork unitOfWork,
    IUniqueViolationTranslator violationTranslator,
    TimeProvider timeProvider)
{
    public async Task<LeaseClaimOutcome> TryClaimAsync(string project, TicketId ticketId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        unitOfWork.Add(new ActiveRun
        {
            Project = project, TicketId = ticketId.Value, ClaimedAt = now, HeartbeatAt = now,
        });
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
            return LeaseClaimOutcome.Claimed;
        }
        catch (DbUpdateException ex) when (violationTranslator.IsUniqueViolation(ex))
        {
            return LeaseClaimOutcome.AlreadyClaimed;
        }
        catch (DbUpdateException)
        {
            return LeaseClaimOutcome.Error;
        }
    }

    public Task ReleaseAsync(string project, TicketId ticketId, CancellationToken ct) =>
        unitOfWork.Set<ActiveRun>()
            .Where(a => a.Project == project && a.TicketId == ticketId.Value)
            .ExecuteDeleteAsync(ct);

    public Task AttachRunAsync(string project, TicketId ticketId, string runId, string? jobId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        return unitOfWork.Set<ActiveRun>()
            .Where(a => a.Project == project && a.TicketId == ticketId.Value)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.RunId, runId)
                .SetProperty(a => a.JobId, jobId)
                .SetProperty(a => a.HeartbeatAt, now), ct);
    }

    public Task RenewHeartbeatAsync(string project, TicketId ticketId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        return unitOfWork.Set<ActiveRun>()
            .Where(a => a.Project == project && a.TicketId == ticketId.Value)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.HeartbeatAt, now), ct);
    }

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
            .Select(r => new StaleLease(r.Project, new TicketId(r.TicketId), r.RunId, r.JobId))
            .ToList();
    }
}
