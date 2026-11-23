using AgentSmith.Application.Services.Passes;
using AgentSmith.Contracts.Passes;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0341c: the OPEN loop's re-engagement. A master that quit early while budget and unfinished work
/// remain is driven on — bounded by money and forward progress, never a fixed re-drive count. Each pass
/// resumes warm. Stops on nothing left, steps still open after their re-drive, the budget, an idle or self-refilling
/// pass, a parked operator question, or the hard safety net.
/// <para>
/// 2026-10-13-9a59f: lifted out of <see cref="AgenticMasterHandler"/>, and every pass it runs is recorded:
/// the pass it was handed (the step's first) ends here with the loop's first decision, each re-engagement
/// pass opens with the reason it was re-engaged for, and every exit ends the open pass with its own word.
/// </para>
/// <para>
/// 2026-11-22-96c8a: the loop carries the texts its passes ended on, which make the step's report; no claim of the
/// master's stops the loop, and the stages decide delivery.
/// </para>
/// </summary>
public sealed class MasterReengagementLoop(ReengagePass passes, MasterPassRecorder recorder, ReengageStopLog stopLog)
{
    // An absolute anti-hang net for the fail-open case (no cost cap configured). It is NOT the control —
    // money and forward progress are.
    internal const int HardSafetyCap = 50;

    internal async Task<ReengageState> RunAsync(
        ReengageSession s, ReengageState state, MasterPassFrame? pass, bool reportWritten, CancellationToken ct)
    {
        for (var n = 0; n < HardSafetyCap; n++)
        {
            // p0374a: pass 0 is the first loop, so a re-engagement pass is 1-based — every ledger
            // transition recorded from here carries the pass it happened in.
            s.SetPass(n + 1);
            var decision = ReengageReasonPolicy.Decide(
                s.PipelineName, s.Progress.GetLedger(), s.CostTracker.IsBudgetExhausted, n + 1);
            if (!decision.Reengage)
            {
                stopLog.NotReengaging(s, n + 1);
                var kind = MasterLoopStop.ClassifyTop(s.CostTracker.IsBudgetExhausted, s.Progress.GetLedger(), n + 1);
                return await StopAsync(s, kind, pass, decision.Reason ?? MasterPassReasons.NothingLeft, state, reportWritten, ct);
            }
            if (s.TicketClarifications?.HasAsked == true) // an operator question short-circuits — the caller parks
                return await StopAsync(s, MasterStopKind.Question, pass, MasterPassReasons.Question, state, reportWritten, ct);

            await EndAsync(s, pass, MasterPassReasons.Reengaged, decision.Reason, state, reportWritten);
            pass = recorder.Open(s.Context.Pipeline, n + 1, decision.Reason);
            ReengagePassResult result;
            try
            {
                result = await passes.RunAsync(s, state, ct);
            }
            finally
            {
                pass?.Dispose();
            }
            (state, reportWritten) = (result.State, result.ReportWritten);
            if (result.Stop is not { } stop) continue;
            var ended = stop switch
            {
                MasterPassReasons.Budget => MasterStopKind.Budget,
                MasterPassReasons.Question => MasterStopKind.Question,
                _ => MasterLoopStop.ClassifyBottom(result.Outcome),
            };
            return await StopAsync(s, ended, pass, stop, state, reportWritten, ct);
        }
        return await StopAsync(s, MasterStopKind.Cap, pass, MasterPassReasons.Cap, state, reportWritten, ct);
    }

    // 2026-10-13-3890a: every loop end is recorded once on the trail, naming the stop.
    private async Task<ReengageState> StopAsync(
        ReengageSession s, MasterStopKind kind, MasterPassFrame? pass, string ended,
        ReengageState state, bool reportWritten, CancellationToken ct)
    {
        if (s.Decisions is not null && MasterLoopStop.TrailLine(s.PipelineName, kind) is { } line)
            await s.Decisions.LogAsync(null, AgentSmith.Contracts.Decisions.DecisionCategory.Implementation, line, ct);
        return await EndAsync(s, pass, ended, null, state, reportWritten);
    }

    private async Task<ReengageState> EndAsync(
        ReengageSession s, MasterPassFrame? pass, string ended, string? reason, ReengageState state,
        bool reportWritten)
    {
        await recorder.EndAsync(s.Context.Pipeline, pass, ended, reason,
            new MasterPassState(s.Progress.GetLedger(), state.Changes, reportWritten, state.Reports.Count > 0));
        return state;
    }
}
