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
        for (var n = 0; n < HardSafetyCap; n++)
        {
            // p0374a: pass 0 is the first loop, so a re-engagement pass is 1-based — every ledger
            // transition recorded from here carries the pass it happened in.
            s.SetPass(n + 1);
            var decision = ReengageReasonPolicy.Decide(
                s.PipelineName, s.Progress.GetLedger(), state.Verification, s.CostTracker.IsBudgetExhausted,
                criteria, state.Changes, n + 1, s.VerdictOwed.Demanded);
            if (!decision.Reengage)
            {
                stopLog.NotReengaging(s, state.Verification, n + 1, criteria.Count);
                return await EndAsync(s, pass, decision.Reason ?? MasterPassReasons.NothingLeft, null, state, emitted);
            }
            if (s.TicketClarifications?.Captured is not null) // an operator question short-circuits — the caller parks
                return await EndAsync(s, pass, MasterPassReasons.Question, null, state, emitted);

            await EndAsync(s, pass, MasterPassReasons.Reengaged, decision.Reason, state, emitted);
            pass = recorder.Open(s.Context.Pipeline, n + 1, decision.Reason);
            ReengagePassResult result;
            try
            {
                result = await passes.RunAsync(s, state, criteria, ct);
            }
            finally
            {
                pass?.Dispose();
            }
            (state, emitted) = (result.State, result.Emitted);
            if (result.Stop is { } stop) return await EndAsync(s, pass, stop, null, state, emitted);
        }
        return await EndAsync(s, pass, MasterPassReasons.Cap, null, state, emitted);
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
