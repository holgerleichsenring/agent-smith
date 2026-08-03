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

    public SpecSequenceProgress With(string phaseId, PhaseRunState state, string? failingCommand = null) =>
        new([.. Phases.Select(p => p.PhaseId == phaseId
            ? p with { State = state, FailingCommand = failingCommand ?? p.FailingCommand }
            : p)]);
}

/// <summary>p0393a: one phase's standing in the sequence.</summary>
public sealed record PhaseProgress(
    string PhaseId,
    string Goal,
    PhaseRunState State,
    string? FailingCommand = null);

/// <summary>p0393a: the three states a reviewer must be able to tell apart.</summary>
public enum PhaseRunState
{
    NotStarted = 0,
    InProgress = 1,
    Done = 2,
    Failed = 3,
}
