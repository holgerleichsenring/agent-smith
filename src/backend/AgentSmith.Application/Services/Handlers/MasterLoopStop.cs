using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Progress;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-13-3890a: why a master's open loop ended, recorded once on the trail of every code run.
/// 2026-11-18-98fa: no stop is challenged any more — what the master reported through finish_spec is its call.
/// </summary>
internal static class MasterLoopStop
{
    /// <summary>Why the loop's top check said stop.</summary>
    public static MasterStopKind ClassifyTop(bool budgetExhausted, MasterVerification? verification, int reengagePass, bool verdictDemanded)
    {
        if (budgetExhausted) return MasterStopKind.Budget;
        if (verification?.Blocker is not null) return MasterStopKind.Blocker;
        if (verification?.Status == VerificationStatus.Failed) return MasterStopKind.RedDrained;
        if (verification is null && ReengageReasonPolicy.UnfinishedAfterOneRedrive(reengagePass, verdictDemanded))
            return MasterStopKind.Verdictless;
        return MasterStopKind.Satisfied;
    }

    /// <summary>Why a pass's own outcome said stop.</summary>
    public static MasterStopKind ClassifyBottom(ReengageOutcome outcome, MasterVerification? verification, ProgressLedger ledger)
    {
        if (outcome == ReengageOutcome.StopBlocked) return MasterStopKind.Blocker;
        if (outcome == ReengageOutcome.StopSelfRefilled) return MasterStopKind.SelfRefilled;
        if (verification?.Status == VerificationStatus.Failed && !ledger.HasActionablePending) return MasterStopKind.RedDrained;
        return MasterStopKind.Idle;
    }

    /// <summary>The trail line, for pipelines that change code; null for the rest.</summary>
    public static string? TrailLine(string? pipelineName, MasterStopKind stop) =>
        string.IsNullOrEmpty(pipelineName) || !PipelinePresets.ExpectsCodeChanges(pipelineName)
            ? null : $"**Master loop ended: {stop}**";
}
