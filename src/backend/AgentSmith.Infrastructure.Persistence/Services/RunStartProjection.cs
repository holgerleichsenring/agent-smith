using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// What RunStarted means for the run row: a new row, a capacity reservation promoted to running, or
/// a parked row relaunched (p0320c / p0327); a duplicate on a running row is a replay and changes
/// nothing.
/// <para>2026-10-13-9a59d: a NEW ATTEMPT announces its ticket — a new code run with a ticket, or the
/// promotion of a row that was queued. A relaunch after a park is the same attempt and stays quiet.
/// Fail-soft: a lost nudge costs a page a refresh, never the row.</para>
/// </summary>
public sealed class RunStartProjection(
    QueuedRunProjection queuedRuns, ILogger<RunStartProjection> logger, ITicketActivityNudge? activity = null)
{
    public async Task ApplyAsync(IUnitOfWork uow, RunStartedEvent e, CancellationToken ct)
    {
        var existing = await uow.Set<Run>().FirstOrDefaultAsync(r => r.Id == e.RunId, ct);
        if (existing is not null)
        {
            if (!RunStatuses.IsWaiting(existing.Status)) return; // duplicate replay
            var wasQueued = existing.Status == RunStatuses.Queued;
            await queuedRuns.PromoteToRunningAsync(uow, existing, e, ct);
            if (wasQueued) await AnnounceAsync(existing, ct);
            return;
        }
        var run = Insert(uow, e);
        await uow.SaveChangesAsync(ct);
        await AnnounceAsync(run, ct);
    }

    private static Run Insert(IUnitOfWork uow, RunStartedEvent e)
    {
        var run = new Run
        {
            Id = e.RunId, Pipeline = e.Pipeline, Trigger = e.Trigger, Status = "running",
            TicketId = e.TicketId ?? string.Empty, AgentName = e.AgentName, StartedAt = e.StartedAt,
            // p0320c: project + platform land on the row so the TOCTOU backstop can key a
            // QueuedTicket entry from the row's own fields.
            Project = e.Project ?? string.Empty, Platform = e.Platform,
            // p0330: the spawn handle — the cancel enforcer force-kills the orchestrator by it.
            JobId = e.JobId,
        };
        uow.Add(run);
        foreach (var repo in e.Repos)
            uow.Add(new RunRepo { RunId = e.RunId, RepoName = repo });
        return run;
    }

    private async Task AnnounceAsync(Run run, CancellationToken ct)
    {
        if (activity is null || run.Pipeline != PipelinePresets.CodeName || string.IsNullOrEmpty(run.TicketId)
            || string.IsNullOrEmpty(run.Platform) || string.IsNullOrEmpty(run.Project)) return;
        try
        {
            await activity.ChangedAsync(run.Project, TicketKey.For(run.Platform, run.TicketId).Value, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Announcing the new attempt {RunId} of its ticket failed", run.Id);
        }
    }
}
