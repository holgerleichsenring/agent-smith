namespace AgentSmith.Contracts.Progress;

/// <summary>
/// p0374a: one recorded change to a ledger entry — what moved, from which state
/// to which, why, and in which master pass. This is the traceability p0374
/// promised when it dropped the merge and never shipped: the ledger's remaining
/// job (p0393) is to tell a watcher what the machine did and when, and a
/// checklist that silently rewrites its own history cannot do it.
/// <para>
/// <see cref="From"/> is null when the entry is new to the ledger;
/// <see cref="To"/> is null when the entry left it. A REFUSED rewrite records
/// From == To == <see cref="ProgressStatus.Done"/> — the attempt happened, the
/// state did not change, and the cause says which attempt it was.
/// </para>
/// </summary>
public sealed record LedgerTransition(
    string EntryId,
    string Activity,
    ProgressStatus? From,
    ProgressStatus? To,
    LedgerTransitionCause Cause,
    int Pass);

/// <summary>p0374a: why a ledger entry moved — the cause every transition carries.</summary>
public enum LedgerTransitionCause
{
    /// <summary>The incoming checklist carries an entry the ledger did not.</summary>
    Added,

    /// <summary>The model moved the entry and the move stands (pending work is its own).</summary>
    ModelUpdate,

    /// <summary>A done entry left done because the model sent the explicit reopen token.</summary>
    ExplicitReopen,

    /// <summary>A rewrite sent a done entry back to pending WITHOUT the reopen token.
    /// Refused: the entry stays done.</summary>
    RegressionRefused,

    /// <summary>A rewrite dropped a done entry. Refused: the entry is re-attached.</summary>
    OmissionRefused,

    /// <summary>A rewrite dropped a pending entry. Allowed: unfinished work is the
    /// model's to restructure.</summary>
    Dropped,
}
