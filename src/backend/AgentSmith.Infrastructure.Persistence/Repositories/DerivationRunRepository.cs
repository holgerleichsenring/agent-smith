using AgentSmith.Contracts.Commands;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-26-bdd0e: the pre-start row of a spec derivation, written at launch so its run id is valid the moment
/// the person gets it. It carries the ticket, so the ticket page can show it; every transition after it is the
/// projector's — RunEventApplier promotes a "queued" row to running.
/// </summary>
public sealed class DerivationRunRepository(IUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    public const string TicketTrigger = "ticket";
    private const string QueuedStatus = "queued";

    public async Task CreateQueuedRunAsync(
        string runId, string project, string ticketId, string platform, IReadOnlyList<string> repos,
        string summary, CancellationToken ct)
    {
        unitOfWork.Add(new Run
        {
            Id = runId, Project = project, Pipeline = PipelinePresets.SpecDeriveName, TicketId = ticketId,
            Platform = platform, Trigger = TicketTrigger, Status = QueuedStatus, Summary = summary,
            StartedAt = timeProvider.GetUtcNow(),
        });
        foreach (var repo in repos)
            unitOfWork.Add(new RunRepo { RunId = runId, RepoName = repo });
        await unitOfWork.SaveChangesAsync(ct);
    }
}
