namespace AgentSmith.Contracts.Specs;

/// <summary>
/// p0393a: the ordered set of phase specs derived from one ticket, carried on the
/// ticket branch.
/// <para>
/// ONE TICKET IS NOT ONE PHASE. An 800-line migration manual across several
/// repositories is a sequence, and the sequence is where termination comes from:
/// each phase ends at its own done-list and its own VerifyPhase, so stopping is
/// structural instead of a judgement the model has to reach.
/// </para>
/// </summary>
public sealed record SpecSet(
    string Key,
    IReadOnlyList<SpecPhase> Phases,
    SpecAccounting Accounting,
    IReadOnlyList<SpecRevision> Revisions,
    SpecSource Source,
    SpecHandback? Handback = null,
    bool TicketPinnedWhole = false,
    IReadOnlyList<string>? ExecutedPhaseIds = null)
{
    /// <summary>
    /// Phase ids that already ran — on this branch, in this run or an earlier one.
    /// An executed phase is APPEND-ONLY: editing one would rewrite the record of work
    /// that already happened and already sits in the branch history, so a correction
    /// to executed work is a NEW phase, never a changed one.
    /// </summary>
    public IReadOnlyList<string> Executed { get; init; } = ExecutedPhaseIds ?? [];

    /// <summary>The phases no run has started yet — the only ones a correction may re-cut.</summary>
    public IReadOnlyList<SpecPhase> UnexecutedTail =>
        [.. Phases.Where(p => !Executed.Contains(p.PhaseId, StringComparer.Ordinal))];

    /// <summary>
    /// Cap on the phases one ticket may split into. The pipeline executor allows a
    /// bounded number of command executions per run and each phase splices its own
    /// plan/master/verify block; beyond this cap a ticket is a programme and belongs
    /// in the spec dialogue, not in one coding run.
    /// </summary>
    public const int MaxPhases = 8;

    /// <summary>The revision the run works from — always the latest.</summary>
    public SpecRevision Current => Revisions[^1];

    /// <summary>True when derivation handed the ticket back instead of specifying it.</summary>
    public bool IsHandedBack => Handback is not null && Handback.Case != SpecHandbackCase.None;
}

/// <summary>
/// p0393a: the header of one revision. Re-entry never regenerates the set — it
/// reads the last revision and writes a new one naming its CAUSE ("comment on
/// ticket", "reviewer edit in PR #N", "re-cut", "resume"). The transform is an LLM
/// and will never be deterministic; reproducibility comes from not repeating it,
/// not from expecting the same answer twice.
/// </summary>
public sealed record SpecRevision(int Number, string Cause, DateTimeOffset At);

/// <summary>
/// p0393a: where the set the run works from came from. The precedence is fixed and
/// a ticket COMMENT is deliberately not among them: after the first run the ticket
/// carries the derived set as a comment, so a rule reading "a ticket carrying a
/// spec skips derivation" would feed the run its own echo — and a comment is
/// editable by anyone, which is precisely the third-party input derivation exists
/// to bound.
/// </summary>
public enum SpecSource
{
    /// <summary>Read back from the ticket branch — the winner whenever it exists.</summary>
    BranchArtifact = 0,

    /// <summary>A spec embedded in the ticket DESCRIPTION — the p0315c phase-ticket shape.</summary>
    TicketDescription = 1,

    /// <summary>Derived from the ticket prose against the analysed repository.</summary>
    Derived = 2,
}
