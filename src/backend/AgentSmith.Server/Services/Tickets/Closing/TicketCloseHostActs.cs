using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Tickets.Closing;

/// <summary>
/// 2026-10-15-114fa: what a close does on the hosts. Every pull request the ticket's runs named is answered once —
/// the records alone miss an unread one and one the refresh has not recorded yet; a record already closed is not
/// tried. The tracker ticket is closed with a resolution carrying agent-smith's own marker, so no webhook or sweep
/// reads it as a person's comment or a rework keyword; a Jira or Azure DevOps tracker with a closed_status moves the
/// ticket there, any other into its done status. A failure is a class, never the host's text.
/// </summary>
public sealed class TicketCloseHostActs(
    IServiceScopeFactory scopes,
    ISourceProviderFactory sources,
    ITicketProviderFactory trackers,
    ILogger<TicketCloseHostActs> logger)
{
    public async Task<IReadOnlyList<TicketPrCloseOutcome>> ClosePullRequestsAsync(
        ResolvedProject project, string ticketId, IReadOnlyList<PrReviewRecordView> records, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        IReadOnlyList<NamedPullRequest> named;
        using (var scope = scopes.CreateScope())
            named = await scope.ServiceProvider.GetRequiredService<NamedPullRequestReader>().ByTicketAsync(project.Name, ticketId, ct);
        var outcomes = new List<TicketPrCloseOutcome>(named.Count);
        foreach (var pr in named)
        {
            var state = records.FirstOrDefault(r => r.PrKey == PrKey.Of(pr.Url))?.State;
            outcomes.Add(state == PrReviewStates.Closed
                ? new TicketPrCloseOutcome(pr.Url, true, TicketCloseReasons.AlreadyClosed)
                : await CloseOneAsync(project, pr, ct));
        }
        return outcomes;
    }

    public async Task<TicketTrackerCloseOutcome> CloseOnTrackerAsync(
        ResolvedProject project, string ticketId, string by, string? note, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        try
        {
            var closed = await CloseAsync(trackers.Create(project.Tracker), project.Tracker, new TicketId(ticketId), Resolution(by, note), ct);
            return closed ? new TicketTrackerCloseOutcome(true, null) : new TicketTrackerCloseOutcome(false, TicketCloseReasons.Refused);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Ticket {Project}/{Ticket} could not be closed on its tracker", project.Name, ticketId);
            return new TicketTrackerCloseOutcome(false, TicketCloseFailureReason.Of(ex));
        }
    }

    /// <summary>The tracker's resolution comment; it starts with agent-smith's own comment marker.</summary>
    public static string Resolution(string by, string? note) =>
        note is null ? $"Agent Smith — closed by {by}" : $"Agent Smith — closed by {by}: {note}";

    private static async Task<bool> CloseAsync(
        ITicketProvider provider, TrackerConnection tracker, TicketId ticket, string resolution, CancellationToken ct)
    {
        if (tracker.Type is not (TrackerType.Jira or TrackerType.AzureDevOps) || string.IsNullOrWhiteSpace(tracker.ClosedStatus))
            return await provider.CloseTicketAsync(ticket, resolution, ct);
        await provider.UpdateStatusAsync(ticket, resolution, ct);
        return await provider.TransitionToAsync(ticket, tracker.ClosedStatus, ct);
    }

    private async Task<TicketPrCloseOutcome> CloseOneAsync(ResolvedProject project, NamedPullRequest pr, CancellationToken ct)
    {
        var repo = project.Repos.FirstOrDefault(r => r.Name == pr.Repo);
        try
        {
            if (repo is null || sources.Create(repo) is not IPullRequestCloser closer)
                return new TicketPrCloseOutcome(pr.Url, false, TicketCloseReasons.Unsupported);
            await closer.CloseAsync(pr.Url, ct);
            logger.LogInformation("Pull request {Url} of {Project}/{Ticket} was closed by a ticket close", pr.Url, project.Name, pr.TicketId);
            return new TicketPrCloseOutcome(pr.Url, true, null);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Pull request {Url} of {Project}/{Ticket} could not be closed", pr.Url, project.Name, pr.TicketId);
            return new TicketPrCloseOutcome(pr.Url, false, TicketCloseFailureReason.Of(ex));
        }
    }
}
