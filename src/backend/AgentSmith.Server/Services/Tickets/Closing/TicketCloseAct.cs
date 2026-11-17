using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Acts;
using AgentSmith.Server.Services.Rework;
using Microsoft.Extensions.DependencyInjection;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace AgentSmith.Server.Services.Tickets.Closing;

/// <summary>
/// 2026-10-15-114fa: a person closes a ticket — "I won't continue", which is not a landing. Refused exactly when
/// the bucket rule's Done arm would answer, while an attempt is live, and — with its pull requests — when one of
/// them merged, since closing the rest would land it. After validation everything runs to the end whatever the
/// request does: the host acts first, then the mark recording what actually closed. Reopen clears the mark only.
/// <para>2026-11-17-e061a: after validation the act is a 'close' run and a reopen a 'reopen' run, each with a run id
/// (<see cref="IActRun"/>); this class validates, reads and maps what the run's step reports.</para>
/// </summary>
public sealed class TicketCloseAct(
    TicketProjects projects, TicketRunsQuery runs, TicketAttempts attempts, FiledTicketRows filed,
    IServiceScopeFactory scopes, ITicketLandingStore landings, ITicketClosedMarkStore marks, IActRun acts)
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
            ?? await ActAsync(resolved, ticketId, key, records, request, by, ct);
    }

    // After validation: one 'close' run does the host acts, the mark and the nudges.
    private async Task<TicketCloseResult> ActAsync(ResolvedProject resolved, string ticketId, string key,
        IReadOnlyList<PrReviewRecordView> records, TicketCloseRequest request, string by, CancellationToken ct)
    {
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        var prs = request.ClosePullRequests ? await PullRequestsAsync(resolved.Name, ticketId, records, ct) : [];
        var slot = new ActSlot<TicketCloseOutcome>();
        var result = await acts.RunAsync(resolved.Name, PipelinePresets.CloseName, new Dictionary<string, object>
        {
            [ContextKeys.CloseTicketRequest] = new CloseTicketRequest(ticketId, Resolution(by, note), prs,
                request.CloseOnTracker, new TicketCloseMarking(key, by, note)),
            [ContextKeys.ActOutcome] = slot,
        });
        return slot.Value is { Mark: { } mark } done
            ? TicketCloseResult.Ok(new TicketCloseAnswer(mark, done.PullRequests, done.Tracker))
            : TicketCloseResult.Refuse(Status500InternalServerError, $"{ticketId} was not closed: the close run ended first ({result.Message}).");
    }

    /// <summary>The tracker's resolution comment; it starts with agent-smith's own comment marker.</summary>
    public static string Resolution(string by, string? note) =>
        note is null ? $"Agent Smith — closed by {by}" : $"Agent Smith — closed by {by}: {note}";

    // Every pull request the ticket's runs named; a record already closed is not tried.
    private async Task<IReadOnlyList<TicketPullRequestToClose>> PullRequestsAsync(
        string project, string ticketId, IReadOnlyList<PrReviewRecordView> records, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var named = await scope.ServiceProvider.GetRequiredService<NamedPullRequestReader>().ByTicketAsync(project, ticketId, ct);
        return [.. named.Select(pr => new TicketPullRequestToClose(pr.Url, pr.Repo,
            records.FirstOrDefault(r => r.PrKey == PrKey.Of(pr.Url))?.State == PrReviewStates.Closed))];
    }

    public async Task<TicketCloseResult> ReopenAsync(string project, string ticketId, CancellationToken ct)
    {
        if (projects.Of(project) is not { } resolved) return TicketCloseResult.Refuse(Status404NotFound, $"No project {project} is configured.");
        var key = TicketLandingRecorder.KeyOf(resolved, ticketId);
        if ((await marks.OfAsync(resolved.Tracker.Name, [key], ct)).Count == 0) return NotClosed(ticketId);
        var slot = new ActSlot<bool>();
        var result = await acts.RunAsync(resolved.Name, PipelinePresets.ReopenName, new Dictionary<string, object>
        {
            [ContextKeys.ReopenTicketRequest] = new ReopenTicketRequest(ticketId, key),
            [ContextKeys.ActOutcome] = slot,
        });
        if (!slot.IsSet)
            return TicketCloseResult.Refuse(Status500InternalServerError, $"{ticketId} was not reopened: the reopen run ended first ({result.Message}).");
        return slot.Value ? TicketCloseResult.Ok(new TicketReopenAnswer(project, ticketId, key)) : NotClosed(ticketId);
    }

    private static TicketCloseResult NotClosed(string ticketId) => TicketCloseResult.Refuse(Status404NotFound, $"{ticketId} is not closed.");

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
}
