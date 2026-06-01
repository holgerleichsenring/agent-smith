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
        // allowed in the policy; pipeline opt-in to fan-out lives at the
        // construction site (only pipelines that pass a SpawnAgentToolHost
        // get spawn_agents). ReadSubAgentObservations is master-and-child
        // safe — included unconditionally so siblings can inspect each
        // other's observations.
        typeof(SpawnAgentToolHost),
        typeof(ReadSubAgentObservationsToolHost),
        // p0191: agent calls get_artifact_credentials on package-manager auth
        // failures. Master + sub-agents both need it (any phase that runs
        // toolchain commands may hit a private feed).
        typeof(GetArtifactCredentialsToolHost),
    };

    public IReadOnlySet<Type> GetAllowedHosts(string pipelineName)
    {
        ArgumentNullException.ThrowIfNull(pipelineName);
        return AllHosts;
    }
}
