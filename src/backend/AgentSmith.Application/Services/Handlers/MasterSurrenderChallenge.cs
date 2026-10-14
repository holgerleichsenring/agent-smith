using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Progress;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-13-3890a: three of the loop's stops take the master's word for it — a red verdict on a
/// drained ledger, a blocker, criteria disposed of as not applicable — and a word needs only to be
/// non-blank. One live master stopped on "dotnet-ef is unavailable", "the regression test was not
/// added" and "three tests remain red" with $43 of budget left. Such a stop, while a ratified
/// criterion is not Met and money remains, gets ONE more pass that puts the master's own reasons in
/// front of it; a stop restated after that is respected. One instance per master invocation.
/// </summary>
internal sealed class MasterSurrenderChallenge
{
    private bool _used;
    private string? _blocker;

    /// <summary>Why the loop's top check said stop. Called only after ShouldReengage returned false.</summary>
    public static MasterStopKind ClassifyTop(
        bool budgetExhausted, MasterVerification? verification, IReadOnlyList<string> criteria,
        int reengagePass, bool verdictDemanded)
    {
        if (budgetExhausted) return MasterStopKind.Budget;
        if (verification?.Status == VerificationStatus.Failed) return MasterStopKind.RedDrained;
        if (MasterAcceptanceGate.VerdictlessAfterOneRedrive(verification, reengagePass, criteria.Count, verdictDemanded))
            return MasterStopKind.Verdictless;
        return DeclinedOnly(verification, criteria) ? MasterStopKind.Declined : MasterStopKind.Satisfied;
    }

    /// <summary>Why a pass's own outcome said stop. A pass with no tool call is read by its verdict first.</summary>
    public MasterStopKind ClassifyBottom(
        ReengageOutcome outcome, MasterVerification? verification, ProgressLedger ledger,
        IReadOnlyList<string> criteria)
    {
        if (outcome == ReengageOutcome.StopBlocked) return MasterStopKind.Blocker;
        if (outcome == ReengageOutcome.StopSelfRefilled) return MasterStopKind.SelfRefilled;
        if (verification?.Status == VerificationStatus.Failed && !ledger.HasActionablePending) return MasterStopKind.RedDrained;
        return DeclinedOnly(verification, criteria) ? MasterStopKind.Declined : MasterStopKind.Idle;
    }

    /// <summary>True — once — when this stop is a surrender worth one more pass. The blocker, if the
    /// master's last answer stated one, goes into the challenge whichever check stopped the loop.</summary>
    public bool TryChallenge(
        MasterStopKind stop, bool budgetExhausted, IReadOnlyList<string> criteria, MasterVerification? verification,
        MasterBlockedClaim? block)
    {
        _blocker = block?.Blocker;
        if (_used || budgetExhausted || criteria.Count == 0 || AllMet(verification, criteria)) return false;
        if (stop is not (MasterStopKind.RedDrained or MasterStopKind.Blocker or MasterStopKind.Declined)) return false;
        Challenged = true;
        return _used = true;
    }

    public bool Challenged { get; private set; }

    public string Nudge(IReadOnlyList<string> criteria, MasterVerification? verification, string originalPrompt) =>
        MasterChallengeNudge.Build(Pairings(verification, criteria), verification, _blocker, originalPrompt);

    /// <summary>The trail line, for pipelines that change code; null for the rest.</summary>
    public async Task RecordAsync(
        IDecisionLogger logger, string? pipelineName, MasterStopKind stop, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(pipelineName) || !PipelinePresets.ExpectsCodeChanges(pipelineName)) return;
        await logger.LogAsync(null, DecisionCategory.Implementation,
            $"**Master loop ended: {stop}**{(Challenged ? " after one challenge pass" : string.Empty)}", ct);
    }

    private static bool AllMet(MasterVerification? verification, IReadOnlyList<string> criteria) =>
        Pairings(verification, criteria).All(p => p.Disposition?.Status == AcceptanceStatus.Met);

    private static bool DeclinedOnly(MasterVerification? verification, IReadOnlyList<string> criteria)
    {
        var pairings = Pairings(verification, criteria);
        return pairings.Count > 0 && pairings.All(p => p.Satisfied)
            && pairings.Any(p => p.Disposition?.Status == AcceptanceStatus.NotApplicable);
    }

    private static IReadOnlyList<CriterionPairing> Pairings(MasterVerification? verification, IReadOnlyList<string> criteria) =>
        AcceptancePairing.Pair(criteria, verification?.AcceptanceDispositions ?? []);
}
