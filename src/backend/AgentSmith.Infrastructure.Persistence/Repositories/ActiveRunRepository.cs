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
    // p0258: a duplicate-claim collision RECLAIMS the lease when the existing one
    // is staler than this. The heartbeat pump (ExecutePipelineUseCase) renews the
    // lease every 45s for the WHOLE run, independent of step progress, so a
    // heartbeat older than this = 4 missed renewals = the server-side run task is
    // gone (process crashed/restarted), NOT a slow-but-live run. Matches the
    // reaper's LeaseFreshFor (3 min) — the reclaim just stops a dead lease from
    // blocking re-claims for the up-to-3-min reaper gap (the "stuck on pending,
    // no job in the UI since relational" regression — the old Redis 2-min TTL
    // self-healed; the DB lease has no TTL).
    private static readonly TimeSpan ReclaimStaleAfter = TimeSpan.FromMinutes(3);

    public async Task<LeaseClaimOutcome> TryClaimAsync(string project, TicketId ticketId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var entry = unitOfWork.Add(new ActiveRun
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
            entry.State = EntityState.Detached; // drop the failed insert before inspecting/reclaiming
            return await TryReclaimStaleAsync(project, ticketId, now, ct);
        }
        catch (DbUpdateException)
        {
            return LeaseClaimOutcome.Error;
        }
    }

    // A lease already exists. If its heartbeat is stale (the run that held it is
    // dead — see ReclaimStaleAfter), take it over by resetting the SAME row to a
    // fresh claim (RunId/JobId cleared — the new run attaches its own). A FRESH
    // lease (live run) still blocks → AlreadyClaimed, preserving single-run. The
    // claim path is serialised per ticket by the Redis claim-lock, so this
    // read-then-update is race-free against another claimer for the same ticket.
    private async Task<LeaseClaimOutcome> TryReclaimStaleAsync(
        string project, TicketId ticketId, DateTimeOffset now, CancellationToken ct)
    {
        // SQLite cannot translate a DateTimeOffset comparison; read the single row
        // and compare client-side (see FindStaleAsync).
        var existing = await unitOfWork.Set<ActiveRun>().AsNoTracking()
            .Where(a => a.Project == project && a.TicketId == ticketId.Value)
            .Select(a => new { a.HeartbeatAt })
            .FirstOrDefaultAsync(ct);
        if (existing is null) return LeaseClaimOutcome.AlreadyClaimed; // released between insert-fail and read
        if (now - existing.HeartbeatAt < ReclaimStaleAfter) return LeaseClaimOutcome.AlreadyClaimed; // live run

        var reclaimed = await unitOfWork.Set<ActiveRun>()
            .Where(a => a.Project == project && a.TicketId == ticketId.Value)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.RunId, (string?)null)
                .SetProperty(a => a.JobId, (string?)null)
                .SetProperty(a => a.ClaimedAt, now)
                .SetProperty(a => a.HeartbeatAt, now), ct);
        return reclaimed > 0 ? LeaseClaimOutcome.Claimed : LeaseClaimOutcome.AlreadyClaimed;
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
