using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0341e / p0406: the objective acceptance gate the open loop re-engages against. Lifted
/// out of MasterReengagementPolicy in p0406 so the gate that decides delivery is its own
/// named thing. Pure predicates over the master's verdict; no state, no collaborators.
/// 2026-09-06-9f14: judges against the criteria themselves, paired by the text each
/// disposition names (<see cref="AcceptancePairing"/>), and says which pairing it used.
/// </summary>
internal static class MasterAcceptanceGate
{
    /// <summary>
    /// The ratified contract is satisfied only when the master's verdict CAN stand for
    /// this kind of phase AND every ratified criterion carries a disposition that is Met
    /// or justified not-applicable. A missing verdict, a disqualifying status, or any
    /// unmet / missing disposition means not satisfied.
    /// </summary>
    internal static bool ObjectivelySatisfied(
        MasterVerification? verification, IReadOnlyList<string> criteria, bool producedSourceChanges) =>
        Judge(verification, criteria, producedSourceChanges).Satisfied;

    /// <summary>
    /// The full judgement. The verdict-level refusals come first and stop before any
    /// pairing — an answer SHORTER than the contract still fails closed, because a master
    /// that did not address every criterion is not done whatever the ones it addressed say.
    /// </summary>
    internal static AcceptanceJudgement Judge(
        MasterVerification? verification, IReadOnlyList<string> criteria, bool producedSourceChanges)
    {
        if (criteria.Count == 0) return AcceptanceJudgement.Of([]);
        if (verification is null) return AcceptanceJudgement.Refused("no verdict");
        if (!StatusAllowsDelivery(verification.Status, producedSourceChanges))
            return AcceptanceJudgement.Refused($"a {verification.Status} verdict cannot stand for this run");
        var dispositions = verification.AcceptanceDispositions;
        if (dispositions is null)
            return AcceptanceJudgement.Refused("the verdict carries no acceptance dispositions");
        if (dispositions.Count < criteria.Count)
            return AcceptanceJudgement.Refused(
                $"{dispositions.Count} disposition(s) for {criteria.Count} criteria — the answer is short");
        return AcceptanceJudgement.Of(AcceptancePairing.Pair(criteria, dispositions));
    }

    // p0406: a phase that produced no source change has nothing for a build to be green
    // ABOUT — run fa8c spent 23 of its 41 minutes on two build invocations trying to
    // reach a gate that could not be reached, and never finished.
    // p0421: which phases those are is READ from the run's own changes rather than
    // declared. A phase that produced source still owes a green build — an Unknown
    // verdict over changed code is exactly the hollow success the gate exists for. A
    // phase that produced none is judged by its dispositions; its own red still
    // disqualifies it, because a master reporting that its work failed is never
    // overruled by what it did or did not touch.
    private static bool StatusAllowsDelivery(VerificationStatus status, bool producedSourceChanges) =>
        producedSourceChanges
            ? status is VerificationStatus.Green or VerificationStatus.NoTests
            : status is not VerificationStatus.Failed;

    /// <summary>
    /// p0406: the master emitted NO verdict and has already had its re-drive for that.
    /// A null verdict reads as "contract not satisfied" forever, so re-driving on it
    /// alone spins until the budget dies (run fa8c: zero Green, zero Failed, zero
    /// dispositions across the whole trail). One pass is the salvage — the nudge may
    /// land; a second is the same prompt against the same silence, so the pass ends on
    /// a named unknown verdict instead.
    /// </summary>
    internal static bool VerdictlessAfterOneRedrive(
        MasterVerification? verification, int reengagePass, int criteriaCount) =>
        verification is null && criteriaCount > 0 && reengagePass > 1;
}
