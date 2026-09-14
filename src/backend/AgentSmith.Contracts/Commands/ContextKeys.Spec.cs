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

    /// <summary>The question the previous run handed the ticket back with, left unanswered,
    /// rendered as a prompt section that pins the taken reading as the answer for this run's
    /// derivation (<see cref="Specs.SpecHandbackCase.Question"/>). Absent when there was no
    /// question, or a person answered it.</summary>
    public const string SpecQuestionPin = "SpecQuestionPin";

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

    /// <summary>p0438: the criteria a phase failed to satisfy, handed back to the master for
    /// one repair pass. Present only while that pass is being prepared.</summary>
    public const string OutstandingCriteria = "OutstandingCriteria";

    /// <summary>p0438: set once a phase has had its repair pass, so a second outstanding
    /// verdict is final rather than a carousel.</summary>
    public const string PhaseRepairAttempted = "PhaseRepairAttempted";

    /// <summary>p0421: every phase's accounts, for the run's one delivery gate.</summary>
    public const string RunAccounts = "RunAccounts";

    /// <summary>p0439: the shortfall CommitAndPR delivered (<see cref="Specs.RunShortfall"/>):
    /// the verified phases are on a ready pull request and the ticket is finalized. Absent
    /// on every run that is not a delivered shortfall.</summary>
    public const string RunShortfall = "RunShortfall";

    /// <summary>p0439: sandbox key → the commit the sandbox stood at, with a clean source
    /// tree, when the last phase was verified (IReadOnlyDictionary&lt;string, string&gt;).
    /// A shortfall delivers exactly that state and proves it against this.</summary>
    public const string VerifiedHeads = "VerifiedHeads";

    /// <summary>
    /// p0422: what the framework staged for the agent — package-feed credentials and
    /// where. An agent that cannot see its own provisioning invents a reason for skipping
    /// the work it needs it for.
    /// </summary>
    public const string StagedRegistries = "StagedRegistries";

    /// <summary>
    /// 2026-09-13-6f35: IReadOnlyList&lt;string&gt; — the template addresses the coding master
    /// may read this phase, each one <c>template:&lt;context&gt;</c>. Its own key because the
    /// repo-name section is built from ContextKeys.SandboxRepos: an entry added to the tool
    /// host alone is reachable and unnameable, and a template must never enter the sandbox map
    /// that CommitAndPR, the toolchain probe and the preflight checks iterate.
    /// </summary>
    public const string TemplateAddresses = "TemplateAddresses";

    /// <summary>
    /// 2026-09-13-7d9f: <see cref="Models.EpicGround"/> — the epic parent this run's ticket
    /// is a slice of, fetched once with the ticket so every child of one cut is derived
    /// against the same stated ground instead of re-inventing it per ticket. Absent when the
    /// ticket carries no parent stamp, and absent when the parent could not be read: an epic
    /// that is gone is reported and the run proceeds on its own ticket, which is a complete
    /// requirement by itself.
    /// </summary>
    public const string EpicGround = "EpicGround";

    /// <summary>p0393a: the ticket segments the derivation was offered, kept so the
    /// accounting and the markdown companions can be rebuilt without re-segmenting.</summary>
    public const string TicketSegments = "TicketSegments";
}
