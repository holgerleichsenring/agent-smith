namespace AgentSmith.Contracts.Commands;

public static partial class ContextKeys
{
    /// <summary>p0390: the CURRENT revision of the work spec (WorkSpecArtifact).
    /// The master's input is this revision — there is no "ratified" spec state,
    /// because introducing one would smuggle a gate back in through vocabulary.</summary>
    public const string WorkSpec = "WorkSpec";

    /// <summary>p0390: the repo of the resolved scope that carries the spec, so a
    /// later run's checkout force-includes it even when the scope changed.</summary>
    public const string WorkSpecRepo = "WorkSpecRepo";

    /// <summary>p0390: sha of the revision this run committed — the pointer's value,
    /// republished for the run record and the viewer.</summary>
    public const string WorkSpecRevisionSha = "WorkSpecRevisionSha";

    /// <summary>p0390: URL of the draft PR opened at the work-spec commit, so the run
    /// surfaces it even when it later parks and never reaches CommitAndPR.</summary>
    public const string WorkSpecPullRequestUrl = "WorkSpecPullRequestUrl";

    /// <summary>p0390: the hand-back the derivation returned (WorkSpecHandback), if any.
    /// The hand-back step routes it: a clarification park for the two question cases,
    /// a verdict park for not-implementable.</summary>
    public const string WorkSpecHandback = "WorkSpecHandback";
}
