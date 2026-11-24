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
        CommandNames.PocPipelineBuild, // 2026-11-23-933dd: the host's pipeline builds, scans and pushes the image
        CommandNames.SealPocVariables, // 2026-11-23-933dg: the secret variables and the pull secret, sealed for the commit
    ];

    /// <summary>2026-11-23-933dg: the run that writes a PoC's variables for its owner, in the PoC's poc- project, started
    /// only through IActRun by the variables route or the import — never queued, because a secret value is in its seed.</summary>
    public const string PocVariablesName = "poc-variables";

    public static readonly IReadOnlyList<string> PocVariables = [CommandNames.WritePocVariables];
}
