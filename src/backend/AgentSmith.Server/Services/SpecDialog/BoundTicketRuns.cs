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
/// <para>
/// 2026-10-13-9a59o: each run carries why it started and each spec what its judge said, and the
/// answer carries the ticket's pull request reviews — composed from <see cref="TicketRunsFacts"/>.
/// </para>
/// </summary>
public sealed class BoundTicketRuns(
    FiledWorkRunsReader runs, TicketRunsFacts facts, ResolvedProject project, string ticketId,
    ILogger logger) : IBoundTicketRuns
{
    public async Task<TicketRunsResult> ForAsync(CancellationToken cancellationToken)
    {
        try
        {
            var found = await runs.ForAsync(
                [new FiledWorkProject(project.Name, project.Tracker.Type.ToString().ToLowerInvariant())],
                ticketId, cancellationToken);
            var read = await facts.ReadAsync(
                project.Name, ticketId, [.. found.Select(run => run.RunId)], cancellationToken);
            return new TicketRunsResult([.. found.Select(run => new TicketRun(
                run.RunId, run.Project, run.Pipeline, run.Status, run.StartedAt,
                read.Causes.GetValueOrDefault(run.RunId), run.PendingQuestion?.Text,
                [.. run.Specs.Select(spec => read.Spec(run.RunId, spec.SpecId, spec.Status, spec.Verdict))],
                [.. run.PullRequests.Select(pr => pr.Url ?? pr.Repo)]))], read.Reviews);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // A store this turn could not read says nothing about what the framework did.
            logger.LogWarning(ex, "The run record for ticket {Ticket} could not be read", ticketId);
            return TicketRunsResult.None;
        }
    }
}
