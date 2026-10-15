using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Rework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace AgentSmith.Server.Services.Tickets.Closing;

/// <summary>
/// 2026-10-15-114fa: a person closes a ticket — "I won't continue", which is not a landing. Refused exactly when
/// the bucket rule's Done arm would answer, while an attempt is live, and — with its pull requests — when one of
/// them merged, since closing the rest would land it. After validation everything runs to the end whatever the
/// request does: the host acts first, then the mark recording what actually closed. Reopen clears the mark only.
/// </summary>
public sealed class TicketCloseAct(
    TicketProjects projects, TicketRunsQuery runs, TicketAttempts attempts, FiledTicketRows filed,
    IServiceScopeFactory scopes, ITicketLandingStore landings, ITicketClosedMarkStore marks, TicketCloseHostActs hosts,
    IReworkNudges nudges, ITicketActivityNudge activity, TimeProvider clock, ILogger<TicketCloseAct> logger)
{
    private const string Running = "running";

    public async Task<TicketCloseResult> CloseAsync(string project, string ticketId, TicketCloseRequest request, string by, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (projects.Of(project) is not { } resolved) return TicketCloseResult.Refuse(Status404NotFound, $"No project {project} is configured.");
        var code = (await runs.ForTicketAsync(project, ticketId, ct)).Where(r => r.Run.Pipeline == PipelinePresets.CodeName).ToList();
        var head = code.Count > 0 ? attempts.Head(code).Run : null;
        if (head is null && await filed.OneAsync(project, ticketId, ct) is null)
            return TicketCloseResult.Refuse(Status404NotFound, $"No ticket {ticketId} is listed in {project}.");
        var key = TicketLandingRecorder.KeyOf(resolved, ticketId);
        IReadOnlyList<PrReviewRecordView> records;
        using (var scope = scopes.CreateScope())
            records = await scope.ServiceProvider.GetRequiredService<IPrReviewRecordReader>().ByTicketAsync(project, ticketId, ct);
        return await RefusalAsync(resolved, ticketId, key, head, records, request, ct)
            ?? TicketCloseResult.Ok(await ActAsync(resolved, ticketId, key, records, request, by));
    }

    // After validation: the host acts, then the mark recording what closed, then the nudges — never cancelled.
    private async Task<TicketCloseAnswer> ActAsync(
        ResolvedProject resolved, string ticketId, string key, IReadOnlyList<PrReviewRecordView> records, TicketCloseRequest request, string by)
    {
        var (project, none) = (resolved.Name, CancellationToken.None);
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        var prs = request.ClosePullRequests ? await hosts.ClosePullRequestsAsync(resolved, ticketId, records, none) : [];
        var onTracker = request.CloseOnTracker ? await hosts.CloseOnTrackerAsync(resolved, ticketId, by, note, none) : null;
        var closed = prs.Where(p => p is { Closed: true, Reason: null }).Select(p => p.Url).ToList();
        var mark = new TicketClosedMark(resolved.Tracker.Name, key, ticketId, project, clock.GetUtcNow(), by, note, closed, onTracker?.Closed == true);
        await marks.SetAsync(mark, none);
        logger.LogInformation("Ticket {Project}/{Ticket} was closed by {By}", project, ticketId, by);
        if (closed.Count > 0) await NudgeAsync(() => nudges.EnqueueAsync(new ReworkNudgeRequest(project, ticketId, ReworkNudgeOrigin.Refresh), none), project, ticketId);
        await NudgeAsync(() => activity.ChangedAsync(project, key, none), project, ticketId);
        return new TicketCloseAnswer(mark, prs, onTracker);
    }

    public async Task<TicketCloseResult> ReopenAsync(string project, string ticketId, CancellationToken ct)
    {
        if (projects.Of(project) is not { } resolved) return TicketCloseResult.Refuse(Status404NotFound, $"No project {project} is configured.");
        var key = TicketLandingRecorder.KeyOf(resolved, ticketId);
        if (!await marks.ClearAsync(resolved.Tracker.Name, key, ct)) return TicketCloseResult.Refuse(Status404NotFound, $"{ticketId} is not closed.");
        logger.LogInformation("Ticket {Project}/{Ticket} was reopened", project, ticketId);
        await NudgeAsync(() => activity.ChangedAsync(project, key, CancellationToken.None), project, ticketId);
        return TicketCloseResult.Ok(new TicketReopenAnswer(project, ticketId, key));
    }

    // The refusals in order: the Done arm, a live attempt, a merged pull request beside the rest.
    private async Task<TicketCloseResult?> RefusalAsync(ResolvedProject resolved, string ticketId, string key, Run? head,
        IReadOnlyList<PrReviewRecordView> records, TicketCloseRequest request, CancellationToken ct)
    {
        var merged = TicketLanding.OfStates([.. records.Select(r => r.State)]) == TicketLanding.Merged ? records.Max(r => r.StateAt) : null;
        var landing = merged is null ? (await landings.LandedAsync(resolved.Tracker.Name, [key], ct)).FirstOrDefault() : null;
        if ((merged ?? landing?.LandedAt) is { } doneAt && (head is null || head.StartedAt <= doneAt))
            return TicketCloseResult.Refuse(Status409Conflict, $"{ticketId} already counts as landed ("
                + $"{(landing?.Kind == TicketLandingKinds.ByHand ? "marked done by hand on" : "merged on")} {doneAt:u}); a landed ticket is done, not closed.");
        if (head?.Status is Running or RunStatuses.Queued or RunStatuses.WaitingForInput)
            return TicketCloseResult.Refuse(Status409Conflict, $"{ticketId} has an attempt running or waiting for an answer; stop it from the run controls first.");
        if (request.ClosePullRequests && records.Any(r => r.State == PrReviewStates.Merged))
            return TicketCloseResult.Refuse(Status409Conflict, $"one of {ticketId}'s pull requests merged; closing the rest would make it land — "
                + "mark it done by hand, or close without closing its pull requests.");
        return null;
    }

    // The mark is stored; a lost nudge only delays a refresh or a page update.
    private async Task NudgeAsync(Func<Task> nudge, string project, string ticketId)
    {
        try { await nudge(); }
        catch (Exception ex) { logger.LogWarning(ex, "Ticket {Project}/{Ticket} is closed but a nudge was lost", project, ticketId); }
    }
}
