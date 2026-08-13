using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0341e / p0406: the objective acceptance gate the open loop re-engages against —
/// mirrored from RunOutcomeKeystone.EvaluateAcceptance, the single definition of done.
/// Lifted out of MasterReengagementPolicy in p0406 so the gate that decides delivery
/// is its own named thing, and so the phase kind it now needs has somewhere to live.
/// Pure predicates over the master's verdict; no state, no collaborators.
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
        MasterVerification? verification, int criteriaCount, bool shipsCode)
    {
        if (criteriaCount == 0) return true;
        if (verification is null) return false;
        if (!StatusAllowsDelivery(verification.Status, shipsCode)) return false;
        var dispositions = verification.AcceptanceDispositions;
        if (dispositions is null || dispositions.Count < criteriaCount) return false;
        for (var i = 0; i < criteriaCount; i++)
        {
            var d = dispositions[i];
            if (d.Status == AcceptanceStatus.Met) continue;
            if (d.Status == AcceptanceStatus.NotApplicable && !string.IsNullOrWhiteSpace(d.Evidence)) continue;
            return false;
        }
        return true;
    }

    // p0406: a knowledge phase (ships_code: false — inventory, classification, a report)
    // produces no source change, so there is nothing for a build to be green ABOUT.
    // Demanding a Green/NoTests status of it demands the one artifact the phase declared
    // it would not make: run fa8c spent 23 of its 41 minutes on two `dotnet build`
    // invocations trying to reach a gate that could not be reached, and never finished.
    // Its dispositions carry the whole verdict — but its OWN red still disqualifies it:
    // a master reporting that its work failed is not overruled by the phase kind.
    private static bool StatusAllowsDelivery(VerificationStatus status, bool shipsCode) =>
        shipsCode
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
