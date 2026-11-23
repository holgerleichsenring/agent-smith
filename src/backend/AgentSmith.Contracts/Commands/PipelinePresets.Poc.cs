namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    /// <summary>2026-11-23-933db: the run that takes one uploaded PoC archive in — it reads the Poc and PocRevision rows
    /// the server wrote at the import, checks the stored archive in process and records the revision validated or
    /// refused; 2026-11-23-933dc: scans it and commits a passed revision to its folder of the source repo. Read only by the static constructor's dictionaries and PocPresets.</summary>
    public const string PocDeployName = "poc-deploy";

    public static readonly IReadOnlyList<string> PocDeploy =
    [
        CommandNames.LoadCatalog, // p0205: every preset binds the skill catalog first
        CommandNames.PocIntake,
        CommandNames.PocCodeScan, // 2026-11-23-933dc: secrets and dependencies, before anything reaches git
        CommandNames.PocSourceCommit,
    ];
}
