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
        typeof(WebToolHost)
    };

    public IReadOnlySet<Type> GetAllowedHosts(string pipelineName)
    {
        ArgumentNullException.ThrowIfNull(pipelineName);
        return AllHosts;
    }
}
