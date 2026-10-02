using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// p0489: data access for the MANUAL init run's row over a SCOPED unit of work.
/// A ticketless init holds no ActiveRun lease (the lease lifecycle is keyed on
/// TicketId), so the run rows themselves are the double-start guard: a
/// non-terminal init run of a project answers "already running" with its id.
/// The pre-start row is written here at launch; every transition after it stays
/// the projector's — RunEventApplier PROMOTES a "queued" row to running.
/// </summary>
public sealed class InitRunRepository(IUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    /// <summary>The trigger ExecutePipelineUseCase stamps on a ticketless run — the
    /// pre-start row carries the same word, never a second concept.</summary>
    public const string ManualTrigger = "manual";

    private const string QueuedStatus = "queued";

    /// <summary>This project's live run of <paramref name="pipeline"/> — unfinished, a
    /// cancel-requested one included — or null when none is in flight.
    /// 2026-10-02-5f89d: a cancelling run still holds its sandboxes until the enforcer ends
    /// it, so it counts as live; skipping it admitted a second init beside them.</summary>
    public Task<Run?> FindLiveRunAsync(string project, string pipeline, CancellationToken ct) =>
        unitOfWork.Set<Run>().AsNoTracking()
            .Where(r => r.Project == project && r.Pipeline == pipeline && r.FinishedAt == null)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync(ct);

    /// <summary>Writes the pre-start row so the launch is immediately visible and
    /// immediately linkable. TicketId stays empty — this run has no ticket and none
    /// is fabricated for it.</summary>
    public async Task CreateQueuedRunAsync(
        string runId, string project, string pipeline, IReadOnlyList<string> repos,
        string summary, CancellationToken ct)
    {
        unitOfWork.Add(new Run
        {
            Id = runId, Project = project, Pipeline = pipeline, TicketId = string.Empty,
            Trigger = ManualTrigger, Status = QueuedStatus, Summary = summary,
            StartedAt = timeProvider.GetUtcNow(),
        });
        foreach (var repo in repos)
            unitOfWork.Add(new RunRepo { RunId = runId, RepoName = repo });
        await unitOfWork.SaveChangesAsync(ct);
    }
}
