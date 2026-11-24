namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    /// <summary>2026-11-23-933db: the run that takes one uploaded PoC archive in — it reads the Poc and PocRevision rows
    /// the server wrote at the import, checks the stored archive in process and records the revision validated or
    /// refused; 2026-11-23-933dc: scans it and commits a passed revision to its folder of the source repo; 933dd builds it; 933dg seals its variables; c81eb commits its manifests to its deploy folder; c81ec follows the rollout. Read only by the static constructor's dictionaries and PocPresets.</summary>
    public const string PocDeployName = "poc-deploy";

    public static readonly IReadOnlyList<string> PocDeploy =
    [
        CommandNames.LoadCatalog, // p0205: every preset binds the skill catalog first
        CommandNames.PocIntake,
        CommandNames.PocCodeScan, // 2026-11-23-933dc: secrets and dependencies, before anything reaches git
        CommandNames.PocSourceCommit,
        CommandNames.PocPipelineBuild, // 2026-11-23-933dd: the host's pipeline builds, scans and pushes the image
        CommandNames.SealPocVariables, // 2026-11-23-933dg: the secret variables and the pull secret, sealed for the commit
        CommandNames.GitOpsCommit, // 2026-11-23-c81eb: the manifests and the sealed files, committed to the deploy folder
        CommandNames.PocRolloutFollow, // 2026-11-23-c81ec: Argo CD's rollout of this render, followed until it settles
    ];

    /// <summary>2026-11-23-933dg: the run that writes a PoC's variables for its owner, in the PoC's poc- project, started
    /// only through IActRun by the variables route or the import — never queued, because a secret value is in its seed.</summary>
    public const string PocVariablesName = "poc-variables";

    public static readonly IReadOnlyList<string> PocVariables = [CommandNames.WritePocVariables];

    /// <summary>2026-11-23-c81ed: the lifecycle acts, each a run in the PoC's poc- project that the server queues from the
    /// detail page: the PocAct step puts the act's desired state into the run, the GitOps commit renders and commits it,
    /// and the follow waits for Argo CD. A delete renders nothing, so it seals nothing.</summary>
    public const string PocStopName = "poc-stop";
    public const string PocStartName = "poc-start";
    public const string PocRollbackName = "poc-rollback";
    public const string PocApplyName = "poc-apply";
    public const string PocDeleteName = "poc-delete";

    public static readonly IReadOnlyList<string> PocRender =
    [
        CommandNames.LoadCatalog, CommandNames.PocAct, CommandNames.SealPocVariables, CommandNames.GitOpsCommit,
        CommandNames.PocRolloutFollow,
    ];

    public static readonly IReadOnlyList<string> PocDelete =
        [CommandNames.LoadCatalog, CommandNames.PocAct, CommandNames.GitOpsCommit, CommandNames.PocRolloutFollow];

    /// <summary>2026-11-23-c81ee: the run that extends a PoC's lifetime, in its poc- project, started only through IActRun
    /// by the extend route so the button gets its answer.</summary>
    public const string PocExtendName = "poc-extend";

    public static readonly IReadOnlyList<string> PocExtend = [CommandNames.PocExtend];
}
