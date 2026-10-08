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
/// </summary>
public sealed class ReworkNudgeHandler(
    IActiveRunLease leases,
    IPreviousAttemptReader attempts,
    IReworkPendingActs pending,
    IReworkEntry rework,
    ReworkSpeech speech,
    ReworkFallbackSpawn fallback,
    ReworkWithheld withheld)
{
    public async Task<ReworkNudgeDisposition> HandleAsync(ResolvedProject project, ClaimedReworkNudge nudge, CancellationToken ct)
    {
        var attempt = await attempts.LatestAsync(project.Name, nudge.TicketId, null, ct);
        if (attempt is null) return ReworkNudgeDisposition.Finish;
        var live = await leases.GetByTicketAsync(project.Name, new TicketId(nudge.TicketId), ct);
        if (live is not null || attempt.Status == RunStatuses.WaitingForInput)
            return await WhileRunningAsync(project, nudge, attempt, live?.RunId ?? attempt.RunId, live is not null, ct);
        if (await attempts.HasQueuedAsync(project.Name, nudge.TicketId, ct)) return ReworkNudgeDisposition.Finish;
        if (!attempt.Finished) return ReworkNudgeDisposition.Reschedule;
        var act = await pending.NewestAsync(project, nudge.TicketId, attempt, ct);
        if (act is null || await withheld.SaysAsync(project, nudge.TicketId, act, attempt, ct)) return ReworkNudgeDisposition.Finish;
        return await EnterAsync(project, nudge, attempt, act, ct);
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

    private async Task<ReworkNudgeDisposition> EnterAsync(
        ResolvedProject project, ClaimedReworkNudge nudge, PreviousAttempt attempt, PendingReworkAct act, CancellationToken ct)
    {
        var outcome = await rework.EnterAsync(project, nudge.TicketId, act.Act, PipelinePresets.CodeName, ct);
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
            case ReworkOutcomeKind.NotARework when nudge.Origin == ReworkNudgeOrigin.Ticket && act.Repo is null:
                await fallback.SpawnAsync(project, nudge.TicketId, ct);
                break;
            case ReworkOutcomeKind.NotARework:
                await speech.SayOnceAsync(project, nudge.TicketId, act, attempt.RunId, "not-a-rework",
                    ReworkTexts.TicketRefusal(NotWaiting), ReworkTexts.PrRefused(nudge.TicketId, NotWaiting), ct);
                break;
        }
        return ReworkNudgeDisposition.Finish;
    }

    private const string NotWaiting = "the ticket is not waiting for one — its last run has not finished, or it is parked as not implementable";
}
