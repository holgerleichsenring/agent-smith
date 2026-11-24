namespace AgentSmith.Contracts.Commands;

/// <summary>2026-11-23-933db: what the server seeds into a PoC run — strings, so the request survives the queue. Every
/// PoC run is told its PoC by <see cref="PocId"/>.</summary>
public static partial class ContextKeys
{
    /// <summary>The Poc row's id.</summary>
    public const string PocId = "PocId";

    /// <summary>The PocRevision number the run takes in.</summary>
    public const string PocRevision = "PocRevision";

    /// <summary>The principal of the person who started the import.</summary>
    public const string PocActor = "PocActor";

    /// <summary>2026-11-23-933dc: "true" when the run lifts a blocked revision (pocs.overrule).</summary>
    public const string PocOverrule = "PocOverrule";

    /// <summary>2026-11-23-933dc: why the overruler lifted the block.</summary>
    public const string PocOverruleReason = "PocOverruleReason";

    /// <summary>2026-11-23-933dc: the blocking finding ids the overrule lifts, a JSON array of strings.</summary>
    public const string PocOverruleIds = "PocOverruleIds";

    /// <summary>2026-11-23-933dc: the source-repo commit that holds the revision's files — what 933dd builds.</summary>
    public const string PocSourceCommitSha = "PocSourceCommitSha";

    /// <summary>2026-11-23-933dd: the pushed image's digest ('sha256:…') the registry confirmed — what c81eb renders.</summary>
    public const string PocImageDigest = "PocImageDigest";

    /// <summary>2026-11-23-933dd: the PoC target a poc-target-install run writes the build pipeline for.</summary>
    public const string PocTargetName = "PocTargetName";

    /// <summary>2026-11-23-933dg: the poc-variables run's seed (PocVariableWrite) — in memory only, never checkpointed.</summary>
    public const string PocVariableWrite = "PocVariableWrite";

    /// <summary>2026-11-23-933dg: the slot the poc-variables step fills with its line, for the route that started it.</summary>
    public const string PocVariableReport = "PocVariableReport";

    /// <summary>2026-11-23-933dg: what SealPocVariables sealed (PocSealResult) — what the GitOps commit writes.</summary>
    public const string PocSeal = "PocSeal";

    /// <summary>2026-11-23-c81eb: the desired state (PocDesiredState) an act's step wants committed; absent, the GitOps
    /// commit reads it from the Poc row (poc-deploy: the run's built revision, one replica).</summary>
    public const string PocDesiredState = "PocDesiredState";

    /// <summary>2026-11-23-c81eb: the GitOps commit's sha; absent when the branch already held the render.</summary>
    public const string PocCommitSha = "PocCommitSha";
}
