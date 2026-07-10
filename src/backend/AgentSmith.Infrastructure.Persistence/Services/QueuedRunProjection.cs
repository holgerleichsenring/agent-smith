using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0320c: the applier's capacity-queue projections, split out of
/// <see cref="RunEventApplier"/>. Two moves: a reserved "queued" Run row is
/// PROMOTED to running when its RunStarted lands (the queued row becomes the
/// running row), and a RunFinished status="queued" (the orchestrator's TOCTOU
/// capacity rejection) UPSERTS a QueuedTicket entry from the run row's own
/// fields so every retry converges on one row + one entry.
/// </summary>
internal static class QueuedRunProjection
{
    public static async Task PromoteToRunningAsync(
        IUnitOfWork uow, Run run, RunStartedEvent e, CancellationToken ct)
    {
        run.Status = "running";
        run.FinishedAt = null;
        run.StartedAt = e.StartedAt;
        run.Pipeline = e.Pipeline;
        run.Trigger = e.Trigger;
        run.AgentName = e.AgentName;
        run.Summary = null; // the waiting reason is obsolete once the run starts
        if (!string.IsNullOrEmpty(e.Project)) run.Project = e.Project;
        if (!string.IsNullOrEmpty(e.Platform)) run.Platform = e.Platform;
        await AddMissingReposAsync(uow, e, ct);
        await uow.SaveChangesAsync(ct);
    }

    // The queued row may already carry repo children (seeded at enqueue) — add
    // only what RunStarted brings on top, never duplicates.
    private static async Task AddMissingReposAsync(IUnitOfWork uow, RunStartedEvent e, CancellationToken ct)
    {
        var known = await uow.Set<RunRepo>()
            .Where(r => r.RunId == e.RunId)
            .Select(r => r.RepoName)
            .ToListAsync(ct);
        foreach (var repo in e.Repos.Except(known, StringComparer.Ordinal))
            uow.Add(new RunRepo { RunId = e.RunId, RepoName = repo });
    }

    public static async Task UpsertEntryAsync(
        IUnitOfWork uow, Run run, DateTimeOffset at, CancellationToken ct)
    {
        // Without a project the entry cannot satisfy UNIQUE(Project, TicketId) —
        // pre-p0320c producers don't stamp it; the poller funnel remains the
        // primary enqueue for those tickets.
        if (string.IsNullOrEmpty(run.Project) || string.IsNullOrEmpty(run.TicketId)) return;

        var entry = await uow.Set<QueuedTicket>()
            .FirstOrDefaultAsync(q => q.Project == run.Project && q.TicketId == run.TicketId, ct);
        if (entry is not null)
        {
            entry.ReservedRunId = run.Id;
            entry.Reason = run.Summary ?? entry.Reason;
            return;
        }
        // InitialContextJson stays null: this entry lacks a trigger envelope, so
        // the poller funnel (which has a fresh one) launches it, not the pump.
        uow.Add(new QueuedTicket
        {
            Project = run.Project, TicketId = run.TicketId, Pipeline = run.Pipeline,
            Platform = run.Platform ?? string.Empty, ReservedRunId = run.Id,
            Reason = run.Summary ?? string.Empty, EnqueuedAt = at,
        });
    }
}
