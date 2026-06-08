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

    public async Task AttachRunAsync(string project, TicketId ticketId, string runId, string? jobId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var updated = await unitOfWork.Set<ActiveRun>()
            .Where(a => a.Project == project && a.TicketId == ticketId.Value)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.RunId, runId)
                .SetProperty(a => a.JobId, jobId)
                .SetProperty(a => a.HeartbeatAt, now), ct);
        if (updated > 0) return;

        // p0252: no lease row yet — a direct-spawn run that never went through the
        // claim (the PR-comment / legacy-webhook path carries a TicketId but does
        // not call TryClaim). INSERT one so EVERY in-flight run holds a lease: the
        // DB lease is the single liveness source, and StaleJobDetector must never
        // see a live but leaseless run as dead. A concurrent claim that inserted
        // first surfaces a unique violation → fall back to the update.
        var entry = unitOfWork.Add(new ActiveRun
        {
            Project = project, TicketId = ticketId.Value,
            RunId = runId, JobId = jobId, ClaimedAt = now, HeartbeatAt = now,
        });
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (violationTranslator.IsUniqueViolation(ex))
        {
            entry.State = EntityState.Detached; // drop the failed insert before retrying
            await unitOfWork.Set<ActiveRun>()
                .Where(a => a.Project == project && a.TicketId == ticketId.Value)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.RunId, runId)
                    .SetProperty(a => a.JobId, jobId)
                    .SetProperty(a => a.HeartbeatAt, now), ct);
        }
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
            .Select(r => new StaleLease(r.Project, new TicketId(r.TicketId), r.RunId, r.JobId, r.HeartbeatAt))
            .ToList();
    }

    public async Task<StaleLease?> GetByTicketAsync(string project, TicketId ticketId, CancellationToken ct)
    {
        var row = await unitOfWork.Set<ActiveRun>().AsNoTracking()
            .Where(a => a.Project == project && a.TicketId == ticketId.Value)
            .Select(a => new { a.Project, a.TicketId, a.RunId, a.JobId, a.HeartbeatAt })
            .FirstOrDefaultAsync(ct);
        return row is null ? null
            : new StaleLease(row.Project, new TicketId(row.TicketId), row.RunId, row.JobId, row.HeartbeatAt);
    }

    public async Task<IReadOnlyCollection<string>> GetActiveRunIdsAsync(TimeSpan freshFor, CancellationToken ct)
    {
        var cutoff = timeProvider.GetUtcNow() - freshFor;
        // SQLite can't translate a DateTimeOffset comparison; the active set is small
        // (one row per in-flight ticket), so filter freshness client-side.
        var rows = await unitOfWork.Set<ActiveRun>().AsNoTracking()
            .Select(a => new { a.RunId, a.HeartbeatAt })
            .ToListAsync(ct);
        return rows
            .Where(r => r.HeartbeatAt >= cutoff && !string.IsNullOrEmpty(r.RunId))
            .Select(r => r.RunId!)
            .ToList();
    }
}
