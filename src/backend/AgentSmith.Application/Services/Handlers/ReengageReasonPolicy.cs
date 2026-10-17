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
/// 2026-10-13-9a59f: names why the master re-engages or stops — exactly the branches
/// <see cref="MasterReengagementPolicy.ShouldReengage"/> always had, in its order. Re-drives: a red
/// verdict with actionable steps, done steps the diff does not back, an unsatisfied acceptance gate, met claims
/// whose citations resolve against nothing (2026-10-17-a196c).
/// Stops: the budget, a red verdict over a drained ledger, a contract still unsatisfied by a master that
/// stayed silent after its one re-drive, and nothing left. The off-code-pipeline stop has no reason: a
/// spec step only runs in the code preset.
/// </summary>
internal static class ReengageReasonPolicy
{
    internal static ReengageDecision Decide(
        string? pipelineName, ProgressLedger ledger, MasterVerification? verification,
        bool budgetExhausted, IReadOnlyList<string> ratifiedCriteria, IReadOnlyList<CodeChange> changes,
        int reengagePass, bool verdictDemandedInPass, IReadOnlyList<string> unresolvedClaims)
    {
        if (string.IsNullOrEmpty(pipelineName) || !PipelinePresets.ExpectsCodeChanges(pipelineName))
            return Stop(null);
        if (budgetExhausted) return Stop(MasterPassReasons.Budget);
        if (verification?.Status == VerificationStatus.Failed)
            return ledger.HasActionablePending
                ? Drive(MasterPassReasons.RedWithOpenSteps)
                : Stop(MasterPassReasons.RedAccepted);
        if (ProgressLedgerCoverage.UnbackedDoneSteps(ledger, changes).Count > 0)
            return Drive(MasterPassReasons.UnbackedDone);
        if (ratifiedCriteria.Count > 0
            && !MasterAcceptanceGate.ObjectivelySatisfied(
                verification, ratifiedCriteria, producedSourceChanges: changes.Count > 0))
            return MasterAcceptanceGate.VerdictlessAfterOneRedrive(
                verification, reengagePass, ratifiedCriteria.Count, verdictDemandedInPass)
                ? Stop(MasterPassReasons.Verdictless)
                : Drive(MasterPassReasons.GateUnsatisfied);
        // 2026-10-17-a196c: a satisfied gate whose met claims cite nothing that changed or ran.
        if (unresolvedClaims.Count > 0) return Drive(MasterPassReasons.UnresolvedClaims);
        return Stop(MasterPassReasons.NothingLeft);
    }

    private static ReengageDecision Drive(string reason) => new(true, reason);

    private static ReengageDecision Stop(string? reason) => new(false, reason);
}
