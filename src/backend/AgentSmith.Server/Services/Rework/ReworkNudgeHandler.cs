using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-08-0781: one nudge, under the ticket's lock. The database decides first and costs
/// nothing: a live run or a waiting one is told about a newer act once and checked when it ends; a
/// queued code run will end and check again; an unfinished attempt is waited for. Only then are
/// the host's acts read, and only a trusted act after the cutoff — and after an operator's stop —
/// goes through the rework entry, whose answer is said once on the act's channel.
/// <para>A run waiting on a hand-back (no checkpoint) is answered by a pull-request act or an operator approval: the entry closes
/// it and starts the rework. A checkpointed question keeps waiting for its answer.</para>
/// <para>2026-10-13-9a59t: on a ticket a person closed an act files a follow-up (<see cref="ClosedTicketActFollowUp"/>),
/// superseding a waiting attempt; an act already followed up is served and never reworks the ticket.</para>
/// </summary>
public sealed class ReworkNudgeHandler(
    IActiveRunLease leases,
    IPreviousAttemptReader attempts,
    IReworkPendingActs pending,
    IReworkEntry rework,
    ReworkSpeech speech,
    ReworkFallbackSpawn fallback,
    ReworkWithheld withheld,
    IRunCheckpointStore checkpoints,
    ClosedTicketActFollowUp followUps)
{
    public async Task<ReworkNudgeDisposition> HandleAsync(ResolvedProject project, ClaimedReworkNudge nudge, CancellationToken ct)
    {
        var attempt = await attempts.LatestAsync(project.Name, nudge.TicketId, null, ct);
        if (attempt is null) return ReworkNudgeDisposition.Finish;
        var live = await leases.GetByTicketAsync(project.Name, new TicketId(nudge.TicketId), ct);
        if (live is null && await HandbackActAsync(project, nudge, attempt, ct) is { } answer)
            return await AnswerAsync(project, nudge, attempt, answer,
                await rework.AnswerHandbackAsync(project, nudge.TicketId, attempt, PipelinePresets.CodeName, ct), ct);
        if (live is null && await ClosedWhileWaitingAsync(project, nudge, attempt, ct) is { } closed) return closed;
        if (live is not null || attempt.Status == RunStatuses.WaitingForInput)
            return await WhileRunningAsync(project, nudge, attempt, live?.RunId ?? attempt.RunId, live is not null, ct);
        if (await attempts.HasQueuedAsync(project.Name, nudge.TicketId, ct)) return ReworkNudgeDisposition.Finish;
        if (!attempt.Finished) return ReworkNudgeDisposition.Reschedule;
        var act = await pending.NewestAsync(project, nudge.TicketId, attempt, ct);
        if (await SpentAsync(project, nudge, act, ct) || await withheld.SaysAsync(project, nudge.TicketId, act!, attempt, ct))
            return ReworkNudgeDisposition.Finish;
        return await AnswerAsync(project, nudge, attempt, act!,
            await rework.EnterAsync(project, nudge.TicketId, act!.Act, PipelinePresets.CodeName, ct), ct);
    }

    // A closed ticket's waiting attempt is answered by nobody; its newest act supersedes it. Spent: no act, or one followed up.
    private async Task<ReworkNudgeDisposition?> ClosedWhileWaitingAsync(
        ResolvedProject project, ClaimedReworkNudge nudge, PreviousAttempt attempt, CancellationToken ct)
    {
        if (attempt.Status != RunStatuses.WaitingForInput) return null;
        var act = await pending.NewestAsync(project, nudge.TicketId, attempt, ct);
        if (await SpentAsync(project, nudge, act, ct) || await withheld.IsWithheldAsync(project, nudge.TicketId, act!, ct)) return null;
        return await rework.SupersedeIfClosedAsync(project, nudge.TicketId, attempt, ct) is { } outcome
            ? await AnswerAsync(project, nudge, attempt, act, outcome, ct) : null;
    }

    // A holder that read its acts is told once about a newer one; one that has not read them yet
    // will read the act itself, so nothing is said. Its end nudges again.
    private async Task<ReworkNudgeDisposition> WhileRunningAsync(
        ResolvedProject project, ClaimedReworkNudge nudge, PreviousAttempt attempt, string? holder, bool leased, CancellationToken ct)
    {
        if (holder is not null && holder == attempt.RunId && attempt.ActsReadAt is not null
            && await pending.NewestAsync(project, nudge.TicketId, attempt, ct) is { } act
            && !await withheld.IsWithheldAsync(project, nudge.TicketId, act, ct))
            await speech.SayOnceAsync(project, nudge.TicketId, act, holder, "live",
                ReworkTexts.TicketLiveRun(holder), ReworkTexts.PrLiveRun(nudge.TicketId, holder), ct);
        return leased ? ReworkNudgeDisposition.Finish : ReworkNudgeDisposition.Reschedule;
    }

    // A hand-back parks waiting_for_input without a checkpoint; a question the ask gate checkpointed
    // keeps its own path back. A pull-request act or an approval in the ticket's conversation answers
    // the hand-back (2026-10-13-9a59m) — read apart from keyword comments, so a later one hides neither.
    private async Task<PendingReworkAct?> HandbackActAsync(
        ResolvedProject project, ClaimedReworkNudge nudge, PreviousAttempt attempt, CancellationToken ct)
    {
        if (attempt.Status != RunStatuses.WaitingForInput || await checkpoints.GetByRunIdAsync(attempt.RunId, ct) is not null)
            return null;
        var act = await pending.NewestHandbackAnswerAsync(project, nudge.TicketId, attempt, ct);
        return !await SpentAsync(project, nudge, act, ct) && !await withheld.SaysAsync(project, nudge.TicketId, act!, attempt, ct) ? act : null;
    }

    private async Task<bool> SpentAsync(ResolvedProject project, ClaimedReworkNudge nudge, PendingReworkAct? act, CancellationToken ct) =>
        act is null || await followUps.IsFollowedUpAsync(project, nudge.TicketId, act, ct);

    private async Task<ReworkNudgeDisposition> AnswerAsync(ResolvedProject project, ClaimedReworkNudge nudge,
        PreviousAttempt attempt, PendingReworkAct act, ReworkOutcome outcome, CancellationToken ct)
    {
        switch (outcome.Kind)
        {
            case ReworkOutcomeKind.Started when outcome.RunId is null:
                return ReworkNudgeDisposition.Reschedule;
            case ReworkOutcomeKind.Started when act.Repo is not null:
                await speech.SayOnceAsync(project, nudge.TicketId, act, outcome.RunId!, "started",
                    string.Empty, ReworkTexts.PrStarted(nudge.TicketId, outcome.RunId), ct);
                break;
            case ReworkOutcomeKind.Refused:
                await speech.SayOnceAsync(project, nudge.TicketId, act, attempt.RunId, "refused",
                    ReworkTexts.TicketRefusal(outcome.Reason!), ReworkTexts.PrRefused(nudge.TicketId, outcome.Reason!), ct);
                break;
            case ReworkOutcomeKind.ClosedForWork:
                return await followUps.AnswerAsync(project, nudge.TicketId, act, attempt.RunId, ct);
            case ReworkOutcomeKind.NotARework when nudge.Origin == ReworkNudgeOrigin.Ticket && act.Repo is null:
                await fallback.SpawnAsync(project, nudge.TicketId, ct);
                break;
            case ReworkOutcomeKind.NotARework:
                await speech.SayOnceAsync(project, nudge.TicketId, act, attempt.RunId, "not-a-rework",
                    ReworkTexts.TicketRefusal(ReworkTexts.NotWaiting), ReworkTexts.PrRefused(nudge.TicketId, ReworkTexts.NotWaiting), ct);
                break;
        }
        return ReworkNudgeDisposition.Finish;
    }
}
