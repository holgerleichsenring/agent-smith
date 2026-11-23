namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    /// <summary>2026-11-23-933db: the run that takes one uploaded PoC archive in — it reads the Poc and PocRevision rows
    /// the server wrote at the import, checks the stored archive in process and records the revision validated or
    /// refused. Nothing is committed in this slice. Read only by the static constructor's dictionaries and PocPresets.</summary>
    public const string PocDeployName = "poc-deploy";

    public static readonly IReadOnlyList<string> PocDeploy =
    [
        CommandNames.LoadCatalog, // p0205: every preset binds the skill catalog first
        CommandNames.PocIntake,
    ];
}
