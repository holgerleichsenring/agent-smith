namespace AgentSmith.Contracts.WorkSpecs;

/// <summary>
/// p0390: the versioned statement of WHAT must be true, derived from the ticket
/// after analysis and carried on the ticket branch as spec.yaml.
/// <para>
/// THE SPEC CARRIES NO STEPS and no target files: naming files is the plan's job
/// (p0276), after the master validated the approach against the code. The ledger
/// keeps seeding from the plan, and the verdict keeps pairing with the p0328
/// expectation — the spec is NOT a gate and has no ratified state.
/// </para>
/// </summary>
public sealed record WorkSpec(
    string Key,
    string Goal,
    IReadOnlyList<string> Requirements,
    IReadOnlyList<WorkSpecConstraint> Constraints,
    IReadOnlyList<string> Done,
    bool DoneIsReadOnly,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<WorkSpecRevision> Revisions,
    WorkSpecHandback? Handback = null)
{
    /// <summary>Cap on requirements — beyond this the ticket is a programme, not a work item.</summary>
    public const int MaxRequirements = 12;

    /// <summary>Cap on verbatim constraints.</summary>
    public const int MaxConstraints = 12;

    /// <summary>Cap on assumptions recorded instead of parking.</summary>
    public const int MaxAssumptions = 10;

    /// <summary>Soft cap: a "requirement" longer than this is prose, not a statement.</summary>
    public const int MaxStatementLength = 500;

    /// <summary>The revision the master works from — always the latest.</summary>
    public WorkSpecRevision Current => Revisions[^1];

    /// <summary>True when derivation handed the ticket back instead of specifying it.</summary>
    public bool IsHandedBack => Handback is not null && Handback.Case != WorkSpecHandbackCase.None;

    public WorkSpec WithRevision(WorkSpecRevision revision) =>
        this with { Revisions = [.. Revisions, revision] };
}
