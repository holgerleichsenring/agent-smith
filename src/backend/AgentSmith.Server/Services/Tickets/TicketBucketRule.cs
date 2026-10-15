using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Events;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59a: who must act next on a ticket, from its newest attempt. A parked attempt
/// needs a person only when <see cref="RunQueuePlace"/> gives it no place — the gate the runs
/// list and the run detail share — so the ticket list never says needs-you while the runs page
/// says queued to resume.
/// <para>Precedence is needs_you > working > closed > waiting_on_review > done > stuck > pr_closed > failed > idle;
/// the pull requests' records decide waiting on review, done and the landing every arm carries.</para>
/// <para>2026-10-13-9a59h: stuck is the stuck rule's observation, passed in — never assumed here. A landed
/// merge outranks it: a person merged what the failed attempt opened.</para>
/// <para>2026-10-15-114fb: a close mark answers closed while <see cref="TicketClosedRule"/> holds it in force.</para>
/// </summary>
public sealed class TicketBucketRule
{
    private const string Running = "running";

    public TicketBucketState Of(
        Run newest, TicketLiveState live, IReadOnlyList<TicketPullRequestView> pullRequests, bool stuck = false,
        DateTimeOffset? landedAt = null, TicketClosedMark? closed = null)
    {
        ArgumentNullException.ThrowIfNull(newest);
        var landing = TicketLanding.Of(pullRequests);
        var place = RunQueuePlace.Of(newest, live.Positions, live.Relaunching);
        if (newest.Status == RunStatuses.WaitingForInput && place is null)
            return NeedsYou(newest, live, landing);
        if (newest.Status is Running or RunStatuses.Queued or RunStatuses.WaitingForInput)
            return new(TicketBucket.Working, null, newest.StartedAt, null, place, null, landing);
        var doneAt = DoneAt(pullRequests, landing) ?? landedAt;
        if (closed is not null && TicketClosedRule.InForce(closed, newest, pullRequests, doneAt))
            return new(TicketBucket.Closed, TicketClosedKinds.ByHand, closed.ClosedAt, newest.Status, null, null, landing);
        if (pullRequests.Any(p => p.State == PrReviewStates.Open))
            return new(TicketBucket.WaitingOnReview, null, newest.FinishedAt, null, null, null, landing);
        if (doneAt is { } at && newest.StartedAt <= at)
            return new(TicketBucket.Done, null, at, null, null, null, landing);
        return Ended(newest, landing, stuck);
    }

    // Done: a pull request merged and every one ended; done at the newest end. An attempt started after
    // it — a keyword comment after a merge — is that attempt's ticket, not a Done one. Else a stored landing's time.
    private static DateTimeOffset? DoneAt(IReadOnlyList<TicketPullRequestView> pullRequests, string? landing) =>
        landing == TicketLanding.Merged ? pullRequests.Max(p => p.StateAt) : null;

    // The arms after done: stuck, then pull requests all closed unmerged, then a failed newest attempt, then the rest.
    private static TicketBucketState Ended(Run newest, string? landing, bool stuck)
    {
        var bucket = stuck ? TicketBucket.Stuck
            : landing == TicketLanding.Unmerged ? TicketBucket.PrClosed
            : newest.Status == RunStatuses.Failed ? TicketBucket.Failed
            : TicketBucket.Idle;
        return new(bucket, null, newest.FinishedAt, newest.Status, null, null, landing);
    }

    private static TicketBucketState NeedsYou(Run newest, TicketLiveState live, string? landing) =>
        live.Pending.GetValueOrDefault(newest.Id) is { } question
            ? new(TicketBucket.NeedsYou, TicketNeedsYouKind.Question, question.AskedAt, null, null, question, landing)
            : new(TicketBucket.NeedsYou, TicketNeedsYouKind.Handback, null, null, null, null, landing);
}
