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
/// <para>Precedence over the series is needs_you > working > waiting_on_review > done > stuck > idle;
/// the pull requests' records decide waiting on review, done and the landing every arm carries.</para>
/// <para>2026-10-13-9a59h: stuck is the stuck rule's observation, passed in — never assumed here. A landed
/// merge outranks it: a person merged what the failed attempt opened.</para>
/// </summary>
public sealed class TicketBucketRule
{
    private const string Running = "running";

    public TicketBucketState Of(
        Run newest, TicketLiveState live, IReadOnlyList<TicketPullRequestView> pullRequests, bool stuck = false,
        DateTimeOffset? landedAt = null)
    {
        ArgumentNullException.ThrowIfNull(newest);
        var landing = TicketLanding.Of(pullRequests);
        var place = RunQueuePlace.Of(newest, live.Positions, live.Relaunching);
        if (newest.Status == RunStatuses.WaitingForInput && place is null)
            return NeedsYou(newest, live, landing);
        if (newest.Status is Running or RunStatuses.Queued or RunStatuses.WaitingForInput)
            return new(TicketBucket.Working, null, newest.StartedAt, null, place, null, landing);
        if (pullRequests.Any(p => p.State == PrReviewStates.Open))
            return new(TicketBucket.WaitingOnReview, null, newest.FinishedAt, null, null, null, landing);
        if ((DoneAt(pullRequests, landing) ?? landedAt) is { } doneAt && newest.StartedAt <= doneAt)
            return new(TicketBucket.Done, null, doneAt, null, null, null, landing);
        if (stuck)
            return new(TicketBucket.Stuck, null, newest.FinishedAt, newest.Status, null, null, landing);
        return new(TicketBucket.Idle, null, newest.FinishedAt, newest.Status, null, null, landing);
    }

    // Done: a pull request merged and every one ended; done at the newest end. An attempt started after
    // it — a keyword comment after a merge — is that attempt's ticket, not a Done one. Else a stored landing's time.
    private static DateTimeOffset? DoneAt(IReadOnlyList<TicketPullRequestView> pullRequests, string? landing) =>
        landing == TicketLanding.Merged ? pullRequests.Max(p => p.StateAt) : null;

    private static TicketBucketState NeedsYou(Run newest, TicketLiveState live, string? landing) =>
        live.Pending.GetValueOrDefault(newest.Id) is { } question
            ? new(TicketBucket.NeedsYou, TicketNeedsYouKind.Question, question.AskedAt, null, null, question, landing)
            : new(TicketBucket.NeedsYou, TicketNeedsYouKind.Handback, null, null, null, null, landing);
}
