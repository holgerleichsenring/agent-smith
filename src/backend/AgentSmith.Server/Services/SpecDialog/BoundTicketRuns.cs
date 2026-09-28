using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-28-1da5d: the framework's own run record for the ticket a conversation is bound to.
/// <para>
/// It reads through the collaborator that already composes runs for the page, addressed by the
/// project and the ticket rather than by a dialog — which is what lets a turn ask about its own.
/// No tracker is called and no credential is resolved: this half is a store read.
/// </para>
/// </summary>
public sealed class BoundTicketRuns(
    FiledWorkRunsReader runs, ResolvedProject project, string ticketId,
    ILogger logger) : IBoundTicketRuns
{
    public async Task<TicketRunsResult> ForAsync(CancellationToken cancellationToken)
    {
        try
        {
            var found = await runs.ForAsync(
                [new FiledWorkProject(project.Name, project.Tracker.Type.ToString().ToLowerInvariant())],
                ticketId, cancellationToken);
            return new TicketRunsResult([.. found.Select(run => new TicketRun(
                run.RunId, run.Project, run.Status, run.StartedAt,
                [.. run.Phases.Select(phase => phase.PhaseId)],
                [.. run.PullRequests.Select(pr => pr.Url ?? pr.Repo)]))]);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // A store this turn could not read says nothing about what the framework did.
            logger.LogWarning(ex, "The run record for ticket {Ticket} could not be read", ticketId);
            return TicketRunsResult.None;
        }
    }
}
