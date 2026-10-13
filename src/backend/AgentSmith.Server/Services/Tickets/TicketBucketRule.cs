using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Events;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59a: who must act next on a ticket, from its newest attempt. A parked attempt
/// needs a person only when <see cref="RunQueuePlace"/> gives it no place — the gate the runs
/// list and the run detail share — so the ticket list never says needs-you while the runs page
/// says queued to resume.
/// <para>2026-10-13-9a59b: precedence over the series is needs_you > working > waiting_on_review >
/// stuck > done > idle; a pull request's record decides waiting on review and done.</para>
/// <para>2026-10-13-9a59h: stuck is the stuck rule's observation, passed in — never assumed here.</para>
/// </summary>
public sealed class TicketBucketRule
{
    private const string Running = "running";

    public TicketBucketState Of(
        Run newest, TicketLiveState live, IReadOnlyList<TicketPullRequestView> pullRequests, bool stuck = false)
    {
        ArgumentNullException.ThrowIfNull(newest);
        ArgumentNullException.ThrowIfNull(pullRequests);
        var place = RunQueuePlace.Of(newest, live.Positions, live.Relaunching);
        if (newest.Status == RunStatuses.WaitingForInput && place is null)
            return NeedsYou(newest, live);
        if (newest.Status is Running or RunStatuses.Queued or RunStatuses.WaitingForInput)
            return new(TicketBucket.Working, null, newest.StartedAt, null, place, null);
        if (pullRequests.Any(p => p.State == PrReviewStates.Open))
            return new(TicketBucket.WaitingOnReview, null, newest.FinishedAt, null, null, null);
        if (stuck)
            return new(TicketBucket.Stuck, null, newest.FinishedAt, newest.Status, null, null);
        if (DoneAt(pullRequests) is { } doneAt && newest.StartedAt <= doneAt)
            return new(TicketBucket.Done, null, doneAt, null, null, null);
        return new(TicketBucket.Idle, null, newest.FinishedAt, newest.Status, null, null);
    }

    // Done: at least one record, every one merged or closed; done at the newest end. An attempt
    // started after it — a keyword comment after a merge — is that attempt's ticket, not a Done one.
    private static DateTimeOffset? DoneAt(IReadOnlyList<TicketPullRequestView> pullRequests) =>
        pullRequests.Count > 0 && pullRequests.All(p => PrReviewStates.IsEnded(p.State))
            ? pullRequests.Max(p => p.StateAt)
            : null;

    private static TicketBucketState NeedsYou(Run newest, TicketLiveState live) =>
        live.Pending.GetValueOrDefault(newest.Id) is { } question
            ? new(TicketBucket.NeedsYou, TicketNeedsYouKind.Question, question.AskedAt, null, null, question)
            : new(TicketBucket.NeedsYou, TicketNeedsYouKind.Handback, null, null, null, null);
}
