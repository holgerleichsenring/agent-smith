using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-08-e8b9e: attaching a run to a ticket's lease never takes it from a LIVE run. Before, the
/// update overwrote whatever row existed, so a run that never claimed (the PR-comment path) took a
/// working run's lease and both worked the ticket. The row is read, judged on the client — unattached,
/// this run's, or stale (SQLite cannot compare a DateTimeOffset in SQL) — and written only WHERE its
/// RunId is still what was read, a compare-and-swap with EF's null semantics. A row that changed in
/// between is judged once more; changed twice, it is held.
/// </summary>
internal sealed class ActiveRunAttach(
    IUnitOfWork unitOfWork, IUniqueViolationTranslator violationTranslator, TimeProvider timeProvider, TimeSpan staleAfter)
{
    public async Task<LeaseAttachOutcome> AttachAsync(string project, TicketId ticketId, string runId, string? jobId, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var row = await unitOfWork.Set<ActiveRun>().AsNoTracking()
                .Where(a => a.Project == project && a.TicketId == ticketId.Value)
                .Select(a => new { a.RunId, a.HeartbeatAt }).FirstOrDefaultAsync(ct);
            if (row is null)
            {
                if (await TryInsertAsync(project, ticketId, runId, jobId, ct)) return LeaseAttachOutcome.Attached;
                continue;
            }
            var now = timeProvider.GetUtcNow();
            if (row.RunId is not null && row.RunId != runId && now - row.HeartbeatAt < staleAfter)
                return LeaseAttachOutcome.HeldByAnotherRun;
            if (await SwapAsync(project, ticketId, row.RunId, runId, jobId, now, ct)) return LeaseAttachOutcome.Attached;
        }
        return LeaseAttachOutcome.HeldByAnotherRun;
    }

    private async Task<bool> SwapAsync(
        string project, TicketId ticketId, string? observed, string runId, string? jobId, DateTimeOffset now, CancellationToken ct) =>
        await unitOfWork.Set<ActiveRun>()
            .Where(a => a.Project == project && a.TicketId == ticketId.Value && a.RunId == observed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.RunId, runId)
                .SetProperty(a => a.JobId, jobId)
                .SetProperty(a => a.HeartbeatAt, now), ct) > 0;

    // p0252: no lease row yet — every in-flight run holds one, so it is inserted. A concurrent claim
    // that inserted first surfaces as a unique violation, and the row is judged again.
    private async Task<bool> TryInsertAsync(string project, TicketId ticketId, string runId, string? jobId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var entry = unitOfWork.Add(new ActiveRun
        {
            Project = project, TicketId = ticketId.Value, RunId = runId, JobId = jobId, ClaimedAt = now, HeartbeatAt = now,
        });
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (violationTranslator.IsUniqueViolation(ex))
        {
            entry.State = EntityState.Detached;
            return false;
        }
    }
}
