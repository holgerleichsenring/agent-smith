using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Default <see cref="IPipelineToolPolicy"/>: every pipeline name (including
/// unknown ones and the '*' wildcard sentinel) gets all three tool hosts
/// active. Preserves the pre-p0145 behaviour for code-family pipelines and
/// keeps the policy fallback safe for tests, custom operator presets, and
/// the transition window before message-family pipelines arrive with their
/// own restrictive policies.
/// </summary>
public sealed class AllHostsActivePolicy : IPipelineToolPolicy
{
    private static readonly IReadOnlySet<Type> AllHosts = new HashSet<Type>
    {
        typeof(FilesystemToolHost),
        typeof(LogDecisionToolHost),
        typeof(HumanToolHost),
        // p0154: WebToolHost is in the allow-list so skills that need web_fetch
        // can resolve it through ToolKit when the construction site provides one.
        // Sites that do not pass a WebToolHost leave web_fetch off the surface.
        typeof(WebToolHost),
        // p0177: SpawnAgentToolHost + ReadSubAgentObservationsToolHost are
        // allowed in the policy; whether a master gets them is decided where its
        // surface is composed (MasterToolComposition): every master whose fan-out
        // count is above zero (max_sub_agents_per_run, or max_sub_agents_per_dialog_turn
        // for a design turn) gets both, and a child gets neither.
        typeof(SpawnAgentToolHost),
        typeof(ReadSubAgentObservationsToolHost),
        // p0191: agent calls get_artifact_credentials on package-manager auth
        // failures. Master + sub-agents both need it (any phase that runs
        // toolchain commands may hit a private feed).
        typeof(GetArtifactCredentialsToolHost),
        // p0193: typed write path for context.yaml. write_file is rejected
        // for those paths; agent must call write_context_yaml instead.
        typeof(WriteContextYamlToolHost),
    };

    public IReadOnlySet<Type> GetAllowedHosts(string pipelineName)
    {
        ArgumentNullException.ThrowIfNull(pipelineName);
        return AllHosts;
    }
}
