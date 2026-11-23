using AgentSmith.Application.Services.Holds;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Events;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59a: what state a ticket is in, from its newest attempt. 2026-11-23-31dcg: who must act next is no longer
/// decided here — <see cref="Held"/> lays the ticket's hold (<see cref="TicketHoldReader"/>) over the state: a person's hold
/// is needs_you, an admin's is stuck. A parked attempt is working here; its hold decides whether a person answers it.
/// <para>Precedence is working > closed > waiting_on_review > done > closed (tracker) > stuck > pr_closed > failed (or cancelled) > done (delivered, no pull request) > idle;
/// the pull requests' records decide waiting on review, done and the landing every arm carries.</para>
/// <para>2026-10-13-9a59h: stuck is the stuck rule's observation, passed in — never assumed here. A landed
/// merge outranks it: a person merged what the failed attempt opened.</para>
/// <para>2026-10-15-114fb: a close mark answers closed while <see cref="TicketClosedRule"/> holds it in force.</para>
/// <para>2026-10-15-114fc: a failed attempt whose own check saw a person's tracker close answers closed (tracker)
/// after done and before stuck.</para>
/// <para>2026-10-16-7ed3a: an open pull request answers waiting on review only while the newest attempt did not fail;
/// a failed head carries its open pull requests into stuck or failed, and a tracker close does not hide them.</para>
/// <para>2026-11-16-f3fbb: a failed head whose end kind is a precondition is stuck by itself, whatever the check saw —
/// 2026-11-23-31dcg: through its admin hold (<see cref="Held"/>), the one reading of a precondition stop.</para>
/// <para>2026-11-17-f9b0a (contract Q2, ratified by the operator 2026-11-17): a cancelled head is failed, and a delivered
/// head that reaches the ended arms — no pull request open, merged or closed for it — is done at its end; idle keeps
/// only what ended neither way.</para>
/// </summary>
public sealed class TicketBucketRule
{
    private const string Running = "running";
    private const string Cancelled = "cancelled";

    public TicketBucketState Of(
        Run newest, TicketLiveState live, IReadOnlyList<TicketPullRequestView> pullRequests, bool stuck = false,
        DateTimeOffset? landedAt = null, TicketClosedMark? closed = null, bool trackerClosed = false)
    {
        ArgumentNullException.ThrowIfNull(newest);
        var landing = TicketLanding.Of(pullRequests);
        var place = RunQueuePlace.Of(newest, live.Positions, live.Relaunching);
        if (newest.Status is Running or RunStatuses.Queued or RunStatuses.WaitingForInput)
            return new(TicketBucket.Working, null, newest.StartedAt, null, place, null, landing);
        var doneAt = DoneAt(pullRequests, landing) ?? landedAt;
        if (closed is not null && TicketClosedRule.InForce(closed, newest, pullRequests, doneAt))
            return new(TicketBucket.Closed, TicketClosedKinds.ByHand, closed.ClosedAt, newest.Status, null, null, landing);
        if (AnyOpen(pullRequests) && newest.Status != RunStatuses.Failed)
            return new(TicketBucket.WaitingOnReview, null, newest.FinishedAt, null, null, null, landing);
        if (doneAt is { } at && newest.StartedAt <= at)
            return new(TicketBucket.Done, null, at, null, null, null, landing);
        return Ended(newest, landing, stuck, trackerClosed && landedAt is null && !AnyOpen(pullRequests));
    }

    private static bool AnyOpen(IReadOnlyList<TicketPullRequestView> pullRequests) =>
        pullRequests.Any(p => p.State == PrReviewStates.Open);

    // Done: a pull request merged and every one ended; done at the newest end. An attempt started after
    // it — a keyword comment after a merge — is that attempt's ticket, not a Done one. Else a stored landing's time.
    private static DateTimeOffset? DoneAt(IReadOnlyList<TicketPullRequestView> pullRequests, string? landing) =>
        landing == TicketLanding.Merged ? pullRequests.Max(p => p.StateAt) : null;

    // The arms after done: a failed attempt a person closed on the tracker with nothing landed, stuck, then pull
    // requests all closed unmerged, then a failed newest attempt, then the rest.
    private static TicketBucketState Ended(Run newest, string? landing, bool stuck, bool trackerClosed)
    {
        if (trackerClosed && newest.Status == RunStatuses.Failed && landing != TicketLanding.Merged)
            return new(TicketBucket.Closed, TicketClosedKinds.Tracker, newest.FinishedAt, newest.Status, null, null, landing);
        // 2026-11-16-f3fbb: a precondition stop is Stuck by what ended it — 2026-11-23-31dcg: as its admin hold, in Held.
        var bucket = stuck ? TicketBucket.Stuck
            : landing == TicketLanding.Unmerged ? TicketBucket.PrClosed
            : newest.Status is RunStatuses.Failed or Cancelled ? TicketBucket.Failed
            : RunStatuses.IsDelivered(newest.Status) ? TicketBucket.Done
            : TicketBucket.Idle;
        return new(bucket, null, newest.FinishedAt, newest.Status, null, null, landing);
    }

    /// <summary>2026-11-23-31dcg: the state under the ticket's hold — a person's hold (merge, plan, decide) is needs_you since
    /// the hold began (else since the state began), a question or a hand-back with its kind and the question; an admin's hold is stuck, so a ticket is never
    /// both stuck and a person's move; any other hold leaves the state as it is.</summary>
    public TicketBucketState Held(TicketBucketState state, TicketHold? hold, PendingQuestionInfo? question = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (hold is null) return state;
        // 2026-11-23-31dci: an escalated wait is stuck since its wait began, the one Stuck it is — the day-old check reads
        // only a failed head, and a wait's head has not failed.
        if (hold.Addressee == TicketHoldAddressees.Admin)
            return state with { Bucket = TicketBucket.Stuck, Since = hold.Since ?? state.Since, QueuePlace = null };
        if (!hold.IsPersons) return state;
        var kind = hold.Case switch
        {
            TicketHoldCases.Question => TicketNeedsYouKind.Question,
            TicketHoldCases.Handback => TicketNeedsYouKind.Handback,
            _ => null,
        };
        return state with
        {
            // A parked attempt waits since its question or hand-back; one without a recorded hand-back has no since.
            Bucket = TicketBucket.NeedsYou, Kind = kind, Since = kind is null ? hold.Since ?? state.Since : hold.Since, QueuePlace = null,
            Question = kind == TicketNeedsYouKind.Question ? question : null,
        };
    }
}
