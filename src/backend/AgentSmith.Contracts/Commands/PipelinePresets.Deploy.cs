namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    /// <summary>2026-11-18-3863c: the run that delivers a version of a context to one environment, per the context's
    /// deployment template: a pull request to the environment's manifest. A person starts it; a manifest write is a
    /// repository write, so it is a run with a run id. Read only by the static constructor's dictionaries.</summary>
    public const string DeployName = "deploy";

    public static readonly IReadOnlyList<string> Deploy =
    [
        CommandNames.LoadCatalog, // p0205: every preset binds the skill catalog first
        CommandNames.DeployToEnvironment,
    ];
}
