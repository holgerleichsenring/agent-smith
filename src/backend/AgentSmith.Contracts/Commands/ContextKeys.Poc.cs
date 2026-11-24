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
}
