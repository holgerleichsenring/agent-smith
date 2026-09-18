namespace AgentSmith.Contracts.Specs;

/// <summary>
/// p0393a: which phases of the sequence are through and which are not.
/// <para>
/// A stopped sequence is a HALF-MIGRATED repository, which is the dangerous state
/// a migration has — worse than not having started. The stop is therefore only half
/// the mechanism: the pull request must carry this table and must be unmergeable by
/// construction. Without it the stop produces exactly the failure it exists to
/// prevent, and it produces it in a form that looks finished.
/// </para>
/// </summary>
public sealed record SpecSequenceProgress(IReadOnlyList<PhaseProgress> Phases)
{
    public static SpecSequenceProgress ForSet(SpecSet set) =>
        new([.. set.Phases.Select(p => new PhaseProgress(p.PhaseId, p.Draft.Goal, PhaseRunState.NotStarted))]);

    /// <summary>True while any phase is anything other than done — the half-migrated state.</summary>
    public bool IsPartial => Phases.Any(p => p.State != PhaseRunState.Done);

    public SpecSequenceProgress With(
        string phaseId, PhaseRunState state, string? failingCommand = null, string? note = null) =>
        new([.. Phases.Select(p => p.PhaseId == phaseId
            ? p with
            {
                State = state,
                FailingCommand = failingCommand ?? p.FailingCommand,
                Note = note ?? p.Note,
            }
            : p)]);
}

/// <summary>p0393a: one phase's standing in the sequence.</summary>
/// <param name="Note">p0460: why the standing is what it is, where the state alone would
/// mislead — a phase found already satisfied on entry is DONE and did no work, and a
/// reader of the table has to be able to tell that from a phase that ran.</param>
public sealed record PhaseProgress(
    string PhaseId,
    string Goal,
    PhaseRunState State,
    string? FailingCommand = null,
    string? Note = null);

/// <summary>p0393a: the states a reviewer must be able to tell apart.</summary>
public enum PhaseRunState
{
    NotStarted = 0,
    InProgress = 1,
    Done = 2,

    /// <summary>The phase ran and its verification came back red.</summary>
    Failed = 3,

    /// <summary>
    /// 2026-09-17-0e79c: the phase did NOT run — it was handed back before its work started,
    /// because what the specification says it rests on is no longer so.
    /// <para>
    /// A separate state, not a flavour of <see cref="Failed"/>, because the two ask opposite
    /// things of the operator: a red build is fixed by working the code, and a false premise is
    /// fixed by amending the specification. A reader that had to tell them apart by parsing the
    /// verdict string would be deriving a fact the producer already knows.
    /// </para>
    /// </summary>
    HandedBack = 4,
}
