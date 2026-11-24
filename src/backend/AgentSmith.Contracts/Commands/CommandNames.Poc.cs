namespace AgentSmith.Contracts.Commands;

/// <summary>2026-11-23-933db: the steps of the PoC runs.</summary>
public static partial class CommandNames
{
    /// <summary>Checks a PoC's uploaded archive in process and records its revision validated or refused.</summary>
    public const string PocIntake = "PocIntakeCommand";

    /// <summary>2026-11-23-933dc: scans a validated PoC revision for secrets and dependency vulnerabilities and blocks it
    /// before anything is committed.</summary>
    public const string PocCodeScan = "PocCodeScanCommand";

    /// <summary>2026-11-23-933dc: commits a passed PoC revision into its folder of the target's source repo.</summary>
    public const string PocSourceCommit = "PocSourceCommitCommand";

    /// <summary>2026-11-23-933dd: has the source repo host's pipeline build, image-scan and push a committed revision, and
    /// reads back its state, scan report and pushed digest.</summary>
    public const string PocPipelineBuild = "PocPipelineBuildCommand";

    /// <summary>2026-11-23-933dd: writes the PoC build pipeline's definition into a target's source repo, once.</summary>
    public const string PocTargetInstall = "PocTargetInstallCommand";
}
