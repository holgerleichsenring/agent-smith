using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Progress;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-13-3890a: why a master's open loop ended, recorded once on the trail of every code run.
/// 2026-11-22-96c8a: no claim of the master's stops the loop; it ends on the budget, on steps still open after their
/// one re-drive, or on nothing left.
/// </summary>
internal static class MasterLoopStop
{
    /// <summary>Why the loop's top check said stop.</summary>
    public static MasterStopKind ClassifyTop(bool budgetExhausted, ProgressLedger ledger, int reengagePass)
    {
        if (budgetExhausted) return MasterStopKind.Budget;
        if (ledger.HasActionablePending && ReengageReasonPolicy.OpenAfterOneRedrive(reengagePass))
            return MasterStopKind.StepsOpen;
        return MasterStopKind.Satisfied;
    }

    /// <summary>Why a pass's own outcome said stop.</summary>
    public static MasterStopKind ClassifyBottom(ReengageOutcome outcome) =>
        outcome == ReengageOutcome.StopSelfRefilled ? MasterStopKind.SelfRefilled : MasterStopKind.Idle;

    /// <summary>The trail line, for pipelines that change code; null for the rest.</summary>
    public static string? TrailLine(string? pipelineName, MasterStopKind stop) =>
        string.IsNullOrEmpty(pipelineName) || !PipelinePresets.ExpectsCodeChanges(pipelineName)
            ? null : $"**Master loop ended: {stop}**";
}
