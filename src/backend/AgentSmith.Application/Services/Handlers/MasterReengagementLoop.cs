using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Passes;
using AgentSmith.Contracts.Passes;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0341c: the OPEN loop's re-engagement. A master that quit early while budget and unfinished work
/// remain is driven on — bounded by money and forward progress, never a fixed re-drive count. Each pass
/// resumes warm. Stops on nothing left, honest red, the budget, an idle or self-refilling pass, a respected
/// blocker, a parked operator question, or the hard safety net.
/// <para>
/// 2026-10-13-9a59f: lifted out of <see cref="AgenticMasterHandler"/>, and every pass it runs is recorded:
/// the pass it was handed (the step's first) ends here with the loop's first decision, each re-engagement
/// pass opens with the reason it was re-engaged for, and every exit ends the open pass with its own word.
/// </para>
/// <para>
/// 2026-10-13-3890a: a stop on the master's own word — red on a drained ledger, a blocker, criteria
/// declined — while a ratified criterion is unmet and money remains gets ONE challenge pass
/// (<see cref="MasterSurrenderChallenge"/>), recorded as a pass opened because it was challenged.
/// </para>
/// </summary>
public sealed class MasterReengagementLoop(ReengagePass passes, MasterPassRecorder recorder, ReengageStopLog stopLog)
{
    // An absolute anti-hang net for the fail-open case (no cost cap configured). It is NOT the control —
    // money and forward progress are.
    internal const int HardSafetyCap = 50;

    internal async Task<ReengageState> RunAsync(
        ReengageSession s, ReengageState state, MasterPassFrame? pass, MasterVerification? emitted, CancellationToken ct)
    {
        var criteria = MasterReengagementPolicy.RatifiedCriteria(s.Context.Pipeline);
        var surrender = new MasterSurrenderChallenge();
        var challenge = false;
        for (var n = 0; n < HardSafetyCap; n++)
        {
            // p0374a: pass 0 is the first loop, so a re-engagement pass is 1-based — every ledger
            // transition recorded from here carries the pass it happened in.
            s.SetPass(n + 1);
            string? reason = MasterPassReasons.Challenged;
            if (!challenge)
            {
                var decision = ReengageReasonPolicy.Decide(
                    s.PipelineName, s.Progress.GetLedger(), state.Verification, s.CostTracker.IsBudgetExhausted,
                    criteria, state.Changes, n + 1, s.VerdictOwed.Demanded);
                reason = decision.Reason;
                if (!decision.Reengage)
                {
                    stopLog.NotReengaging(s, state.Verification, n + 1, criteria.Count);
                    var kind = MasterSurrenderChallenge.ClassifyTop(s.CostTracker.IsBudgetExhausted, state.Verification,
                        criteria, n + 1, s.VerdictOwed.Demanded);
                    challenge = surrender.TryChallenge(kind, s.CostTracker.IsBudgetExhausted, criteria, state.Verification,
                        MasterVerificationParser.TryParseBlockedClaim(state.LoopResult.Response.Text));
                    if (!challenge)
                        return await StopAsync(s, surrender, kind, pass, decision.Reason ?? MasterPassReasons.NothingLeft, state, emitted, ct);
                    reason = MasterPassReasons.Challenged;
                }
            }
            if (s.TicketClarifications?.Captured is not null) // an operator question short-circuits — the caller parks
                return await StopAsync(s, surrender, MasterStopKind.Question, pass, MasterPassReasons.Question, state, emitted, ct);

            await EndAsync(s, pass, MasterPassReasons.Reengaged, reason, state, emitted);
            pass = recorder.Open(s.Context.Pipeline, n + 1, reason);
            var nudge = challenge ? surrender.Nudge(criteria, state.Verification, s.UserPrompt) : null;
            challenge = false;
            ReengagePassResult result;
            try
            {
                result = await passes.RunAsync(s, state, criteria, ct, nudge);
            }
            finally
            {
                pass?.Dispose();
            }
            (state, emitted) = (result.State, result.Emitted);
            if (result.Stop is not { } stop) continue;
            var ended = stop switch
            {
                MasterPassReasons.Budget => MasterStopKind.Budget,
                MasterPassReasons.Question => MasterStopKind.Question,
                _ => surrender.ClassifyBottom(result.Outcome, state.Verification, s.Progress.GetLedger(), criteria),
            };
            challenge = ended is not (MasterStopKind.Budget or MasterStopKind.Question) && surrender.TryChallenge(
                ended, s.CostTracker.IsBudgetExhausted, criteria, state.Verification, result.Block);
            if (!challenge) return await StopAsync(s, surrender, ended, pass, stop, state, emitted, ct);
        }
        return await StopAsync(s, surrender, MasterStopKind.Cap, pass, MasterPassReasons.Cap, state, emitted, ct);
    }

    // 2026-10-13-3890a: every loop end is recorded once on the trail, naming the stop.
    private async Task<ReengageState> StopAsync(
        ReengageSession s, MasterSurrenderChallenge surrender, MasterStopKind kind, MasterPassFrame? pass, string ended,
        ReengageState state, MasterVerification? emitted, CancellationToken ct)
    {
        if (s.Decisions is not null) await surrender.RecordAsync(s.Decisions, s.PipelineName, kind, ct);
        return await EndAsync(s, pass, ended, null, state, emitted);
    }

    private async Task<ReengageState> EndAsync(
        ReengageSession s, MasterPassFrame? pass, string ended, string? reason, ReengageState state,
        MasterVerification? emitted)
    {
        await recorder.EndAsync(s.Context.Pipeline, pass, ended, reason,
            new MasterPassState(s.Progress.GetLedger(), state.Changes, emitted, state.Verification));
        return state;
    }
}
