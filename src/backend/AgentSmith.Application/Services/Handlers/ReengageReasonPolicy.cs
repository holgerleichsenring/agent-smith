using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Passes;
using AgentSmith.Contracts.Progress;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>2026-10-13-9a59f: whether the open loop re-engages, and the reason it gives either way.</summary>
/// <param name="Reason">A <see cref="MasterPassReasons"/> word; null only off a code pipeline.</param>
public sealed record ReengageDecision(bool Reengage, string? Reason);

/// <summary>
/// 2026-10-13-9a59f: names why the master re-engages or stops. 2026-11-22-96c8a: no verdict exists any more — the
/// master's report is its last words and the stages decide delivery. A master that ended with actionable checklist
/// steps open is driven once more for them ('unfinished'); still open after that, the loop stops ('steps-open'). A
/// drained ledger stops ('nothing-left') whether or not a report was written. Two spends shifted with it: a master
/// that once reported green through the closing tool over open steps stopped and is now re-driven once; one that ended text-only over
/// a drained ledger once got the verdict nudge and a re-drive and now gets none.
/// 2026-11-19-9c6e: a done step the diff does not show does not re-drive; its coverage is still reported on the run.
/// </summary>
internal static class ReengageReasonPolicy
{
    internal static ReengageDecision Decide(
        string? pipelineName, ProgressLedger ledger, bool budgetExhausted, int reengagePass)
    {
        if (string.IsNullOrEmpty(pipelineName) || !PipelinePresets.ExpectsCodeChanges(pipelineName))
            return Stop(null);
        if (budgetExhausted) return Stop(MasterPassReasons.Budget);
        if (ledger.HasActionablePending)
            return OpenAfterOneRedrive(reengagePass)
                ? Stop(MasterPassReasons.StepsOpen)
                : Drive(MasterPassReasons.Unfinished);
        return Stop(MasterPassReasons.NothingLeft);
    }

    /// <summary>
    /// p0406: open steps have had their one re-drive — a second is the same prompt against the same answer.
    /// </summary>
    internal static bool OpenAfterOneRedrive(int reengagePass) => reengagePass > 1;

    private static ReengageDecision Drive(string reason) => new(true, reason);

    private static ReengageDecision Stop(string? reason) => new(false, reason);
}
