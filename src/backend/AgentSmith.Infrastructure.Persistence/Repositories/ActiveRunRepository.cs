using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
    TimeProvider timeProvider,
    ILogger<ActiveRunRepository> logger)
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
    private static readonly TimeSpan RunlessDelay = TimeSpan.FromMinutes(1);

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
        // and compare client-side (as ActiveRunLivenessRepository does).
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

    // p0459: the DELETE is conditional on the HOLDER. Two runs worked one ticket
    // live because a ticket-keyed release let a deleted run drop the lease a
    // NEWER run held. runId null means "the claim no run has taken up yet" and
    // matches an unattached row only — widening it to "any holder" would rebuild
    // the same defect. A refusal is logged here, once, for all six call sites.
    // 2026-10-08-0781: the release also asks the rework worker to check the ticket — in ONE
    // transaction with the delete, so no reader sees the nudge while the lease still stands, and a
    // refused release leaves no nudge. A release no run held (a rolled-back claim) waits a minute.
    public async Task<LeaseReleaseOutcome> ReleaseAsync(
        string project, TicketId ticketId, string? runId, CancellationToken ct)
    {
        await using (var tx = await unitOfWork.BeginTransactionAsync(ct))
        {
            await new ReworkNudgeWriter(unitOfWork, violationTranslator).UpsertAsync(new ReworkNudgeRequest(
                project, ticketId.Value, ReworkNudgeOrigin.RunEnd, Delay: runId is null ? RunlessDelay : null), timeProvider.GetUtcNow(), ct);
            var deleted = await unitOfWork.Set<ActiveRun>()
                .Where(a => a.Project == project && a.TicketId == ticketId.Value && a.RunId == runId)
                .ExecuteDeleteAsync(ct);
            if (deleted > 0)
            {
                await tx.CommitAsync(ct);
                return LeaseReleaseOutcome.Released;
            }
            await tx.RollbackAsync(ct);
        }

        var held = await GetByTicketAsync(project, ticketId, ct);
        if (held is null) return LeaseReleaseOutcome.NotFound;
        logger.LogWarning(
            "Run {RunId} asked to release the lease for {Project}/{Ticket} but run {Holder} holds it — "
            + "keeping the holder's claim", runId ?? "—", project, ticketId.Value, held.RunId ?? "—");
        return LeaseReleaseOutcome.HeldByAnotherRun;
    }

    // 2026-10-08-e8b9e: never over a live run's lease — see ActiveRunAttach.
    public Task<LeaseAttachOutcome> AttachRunAsync(string project, TicketId ticketId, string runId, string? jobId, CancellationToken ct) =>
        new ActiveRunAttach(unitOfWork, violationTranslator, timeProvider, ReclaimStaleAfter).AttachAsync(project, ticketId, runId, jobId, ct);

    public Task RenewHeartbeatAsync(string project, TicketId ticketId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        return unitOfWork.Set<ActiveRun>()
            .Where(a => a.Project == project && a.TicketId == ticketId.Value)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.HeartbeatAt, now), ct);
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
}
