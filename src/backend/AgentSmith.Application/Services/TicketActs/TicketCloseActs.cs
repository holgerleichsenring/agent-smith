using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.TicketActs;

/// <summary>
/// 2026-11-17-e061a: the 'close' run's act, everything the server did after it validated a close: the host acts first,
/// then — for a person's close — the mark recording what actually closed, then the nudges. It runs on no token: the
/// close was accepted, and a close broken off between the host and the mark would leave a closed ticket unrecorded.
/// </summary>
public sealed class TicketCloseActs(
    TicketHostClose hosts, ITicketClosedMarkStore marks, IReworkNudges nudges, ITicketActivityNudge activity,
    TimeProvider clock, ILogger<TicketCloseActs> logger)
{
    public async Task<TicketCloseOutcome> CloseAsync(ResolvedProject project, CloseTicketRequest request)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(request);
        var none = CancellationToken.None;
        var prs = await hosts.ClosePullRequestsAsync(project, request.PullRequests, none);
        var onTracker = request.CloseOnTracker ? await hosts.CloseOnTrackerAsync(project, request.TicketId, request.Resolution, none) : null;
        if (request.Mark is not { } marking) return new TicketCloseOutcome(prs, onTracker, null);
        var closed = prs.Where(p => p is { Closed: true, Reason: null }).Select(p => p.Url).ToList();
        var mark = new TicketClosedMark(project.Tracker.Name, marking.Key, request.TicketId, project.Name, clock.GetUtcNow(),
            marking.By, marking.Note, closed, onTracker?.Closed == true);
        await marks.SetAsync(mark, none);
        logger.LogInformation("Ticket {Project}/{Ticket} was closed by {By}", project.Name, request.TicketId, marking.By);
        if (closed.Count > 0)
            await NudgeAsync(() => nudges.EnqueueAsync(new ReworkNudgeRequest(project.Name, request.TicketId, ReworkNudgeOrigin.Refresh), none), project.Name, request.TicketId);
        await NudgeAsync(() => activity.ChangedAsync(project.Name, marking.Key, none), project.Name, request.TicketId);
        return new TicketCloseOutcome(prs, onTracker, mark);
    }

    // The mark is stored; a lost nudge only delays a refresh or a page update.
    private async Task NudgeAsync(Func<Task> nudge, string project, string ticketId)
    {
        try { await nudge(); }
        catch (Exception ex) { logger.LogWarning(ex, "Ticket {Project}/{Ticket} is closed but a nudge was lost", project, ticketId); }
    }
}
