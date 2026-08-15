namespace AgentSmith.Contracts.Commands;

public static partial class ContextKeys
{
    /// <summary>p0393a: the ordered set of phase specs this run works through
    /// (<see cref="Specs.SpecSet"/>), whatever its source. PhaseSpecGate publishes it;
    /// the sequence splices one plan/master/verify block per phase out of it.</summary>
    public const string SpecSet = "SpecSet";

    /// <summary>p0393a: the repo of the resolved scope that carries the spec set, so a
    /// later run's checkout force-includes it even when the scope changed.</summary>
    public const string SpecRepo = "SpecRepo";

    /// <summary>p0393a: sha of the revision this run committed — the pointer's value,
    /// republished for the run record and the viewer.</summary>
    public const string SpecRevisionSha = "SpecRevisionSha";

    /// <summary>p0393a: URL of the draft pull request opened at the spec commit, so the
    /// run surfaces it even when it later parks and never reaches CommitAndPR.</summary>
    public const string SpecPullRequestUrl = "SpecPullRequestUrl";

    /// <summary>p0393a: the hand-back derivation returned (<see cref="Specs.SpecHandback"/>),
    /// if any. The hand-back step routes it: a clarification park for the contradiction
    /// case, a verdict park for not-implementable.</summary>
    public const string SpecHandback = "SpecHandback";

    /// <summary>p0393a: which phases of the sequence are through and which are not
    /// (<see cref="Specs.SpecSequenceProgress"/>). A stopped sequence leaves a
    /// half-migrated repository, and the pull request must state it per phase.</summary>
    public const string SpecSequenceProgress = "SpecSequenceProgress";

    /// <summary>
    /// p0420: one SpecAccount per repository — the ratified criteria against what the
    /// branch delivers, with the file each satisfied criterion is satisfied by. The
    /// pull request renders it, so a reviewer refutes a claim instead of re-deriving it.
    /// </summary>
    public const string PhaseAccounts = "PhaseAccounts";

    /// <summary>p0421: every phase's accounts, for the run's one delivery gate.</summary>
    public const string RunAccounts = "RunAccounts";

    /// <summary>p0393a: the ticket segments the derivation was offered, kept so the
    /// accounting and the markdown companions can be rebuilt without re-segmenting.</summary>
    public const string TicketSegments = "TicketSegments";
}
