namespace AgentSmith.Domain.Models;

/// <summary>
/// How the spec review disposed of ONE criterion of a derived phase, BEFORE any work
/// exists to judge it against.
/// <para>
/// The delivery account (<see cref="AccountDisposition"/>) asks whether a diff satisfies a
/// criterion and can answer only satisfied, not applicable, or not satisfied. A criterion
/// that NO diff could ever satisfy is indistinguishable from one the work has yet to reach:
/// both are outstanding, and the phase is handed back for a repair pass that cannot close
/// it. This enum exists for the distinction that vocabulary cannot make.
/// </para>
/// <para>
/// <see cref="Decidable"/> is the floor and the default for everything the review could not
/// settle. The bias is deliberate and the opposite of the account's: a wrong "not satisfied"
/// costs a repair pass, while a wrong finding here costs a human's next working day and
/// teaches them to stop reading the channel. What cannot be demonstrated passes.
/// </para>
/// </summary>
public enum SpecReviewDisposition
{
    /// <summary>The criterion states the world after the work and an observation settles it.
    /// The floor: everything the review could not demonstrate a defect in lands here.</summary>
    Decidable = 0,

    /// <summary>The criterion prescribes the SHAPE of the solution — which files change, what
    /// the fix looks like — and the repository is free to refuse it however good the work is.
    /// </summary>
    PrescribesShape = 1,

    /// <summary>No observation settles the criterion: it rests on a judgement ("the ones worth
    /// fixing") rather than a state anything can look at.</summary>
    NoObservationSettles = 2,

    /// <summary>The criterion already holds before any work — a no-op criterion, or a sign the
    /// phase has nothing left to do.</summary>
    AlreadyTrue = 3,
}
