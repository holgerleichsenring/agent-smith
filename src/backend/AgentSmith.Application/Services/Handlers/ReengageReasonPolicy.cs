using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Passes;
using AgentSmith.Contracts.Progress;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>2026-10-13-9a59f: whether the open loop re-engages, and the reason it gives either way.</summary>
/// <param name="Reason">A <see cref="MasterPassReasons"/> word; null only off a code pipeline.</param>
public sealed record ReengageDecision(bool Reengage, string? Reason);

/// <summary>
/// 2026-10-13-9a59f: names why the master re-engages or stops. Re-drives: a red verdict with actionable steps and a
/// master that has not called finish_spec (once). Stops: the budget, a blocker,
/// a red verdict over a drained ledger, a master still without finish_spec after its one re-drive, and nothing left.
/// 2026-11-18-98fa: nothing is judged per done line — what the master reported is its call.
/// 2026-11-19-9c6e: a done step the diff does not show no longer re-drives: a master whose child built the work
/// was re-driven five times after a green finish_spec (live replay). The coverage is still reported on the run.
/// </summary>
internal static class ReengageReasonPolicy
{
    internal static ReengageDecision Decide(
        string? pipelineName, ProgressLedger ledger, MasterVerification? verification,
        bool budgetExhausted, IReadOnlyList<CodeChange> changes, int reengagePass)
    {
        if (string.IsNullOrEmpty(pipelineName) || !PipelinePresets.ExpectsCodeChanges(pipelineName))
            return Stop(null);
        if (budgetExhausted) return Stop(MasterPassReasons.Budget);
        if (verification?.Blocker is not null) return Stop(MasterPassReasons.Blocked);
        if (verification?.Status == VerificationStatus.Failed)
            return ledger.HasActionablePending
                ? Drive(MasterPassReasons.RedWithOpenSteps)
                : Stop(MasterPassReasons.RedAccepted);
        if (verification is null)
            return UnfinishedAfterOneRedrive(reengagePass)
                ? Stop(MasterPassReasons.Verdictless)
                : Drive(MasterPassReasons.Unfinished);
        return Stop(MasterPassReasons.NothingLeft);
    }

    /// <summary>
    /// p0406: a master that never called finish_spec has had its one re-drive for it — a second is the same prompt
    /// against the same silence.
    /// </summary>
    internal static bool UnfinishedAfterOneRedrive(int reengagePass) => reengagePass > 1;

    private static ReengageDecision Drive(string reason) => new(true, reason);

    private static ReengageDecision Stop(string? reason) => new(false, reason);
}
