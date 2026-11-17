using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.TicketActs;

/// <summary>
/// 2026-10-15-114fa (2026-11-17-e061a: moved from the server's TicketCloseHostActs into the 'close' run): what a close
/// does on the hosts. Every named pull request is answered once, a record already closed is not tried. The tracker
/// ticket is closed with a resolution carrying agent-smith's own marker, so no webhook or sweep reads it as a person's
/// comment or a rework keyword; a Jira or Azure DevOps tracker with a closed_status moves the ticket there, any other
/// into its done status. A failure is a class, never the host's text.
/// </summary>
public sealed class TicketHostClose(
    ISourceProviderFactory sources, ITicketProviderFactory trackers, ITicketCloseFailureClassifier failures,
    ILogger<TicketHostClose> logger)
{
    public async Task<IReadOnlyList<TicketPrCloseOutcome>> ClosePullRequestsAsync(
        ResolvedProject project, IReadOnlyList<TicketPullRequestToClose> pullRequests, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        var outcomes = new List<TicketPrCloseOutcome>(pullRequests.Count);
        foreach (var pr in pullRequests)
            outcomes.Add(pr.AlreadyClosed
                ? new TicketPrCloseOutcome(pr.Url, true, TicketCloseReasons.AlreadyClosed)
                : await CloseOneAsync(project, pr, ct));
        return outcomes;
    }

    public async Task<TicketTrackerCloseOutcome> CloseOnTrackerAsync(
        ResolvedProject project, string ticketId, string resolution, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        try
        {
            var closed = await CloseAsync(trackers.Create(project.Tracker), project.Tracker, new TicketId(ticketId), resolution, ct);
            return closed ? new TicketTrackerCloseOutcome(true, null) : new TicketTrackerCloseOutcome(false, TicketCloseReasons.Refused);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Ticket {Project}/{Ticket} could not be closed on its tracker", project.Name, ticketId);
            return new TicketTrackerCloseOutcome(false, failures.Of(ex));
        }
    }

    private static async Task<bool> CloseAsync(
        ITicketProvider provider, TrackerConnection tracker, TicketId ticket, string resolution, CancellationToken ct)
    {
        if (tracker.Type is not (TrackerType.Jira or TrackerType.AzureDevOps) || string.IsNullOrWhiteSpace(tracker.ClosedStatus))
            return await provider.CloseTicketAsync(ticket, resolution, ct);
        await provider.UpdateStatusAsync(ticket, resolution, ct);
        return await provider.TransitionToAsync(ticket, tracker.ClosedStatus, ct);
    }

    private async Task<TicketPrCloseOutcome> CloseOneAsync(ResolvedProject project, TicketPullRequestToClose pr, CancellationToken ct)
    {
        var repo = project.Repos.FirstOrDefault(r => r.Name == pr.Repo);
        try
        {
            if (repo is null || sources.Create(repo) is not IPullRequestCloser closer)
                return new TicketPrCloseOutcome(pr.Url, false, TicketCloseReasons.Unsupported);
            await closer.CloseAsync(pr.Url, ct);
            logger.LogInformation("Pull request {Url} of {Project} was closed by a ticket close", pr.Url, project.Name);
            return new TicketPrCloseOutcome(pr.Url, true, null);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Pull request {Url} of {Project} could not be closed", pr.Url, project.Name);
            return new TicketPrCloseOutcome(pr.Url, false, failures.Of(ex));
        }
    }
}
