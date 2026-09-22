using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Configuration.Resolved;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>
/// 2026-09-22-6968: runs the EXISTING resolution against a project whose sandbox block is
/// empty. The same code, one different input — writing a parallel "inheritance resolver"
/// would be a second answer able to disagree with the first, which is the defect the single
/// resolution pass was consolidated to end.
/// </summary>
public sealed class InheritedSandboxProjection(
    IConfigResolver resolver,
    IAgentVersionResolver versions,
    IOptions<SandboxGlobalConfig> global,
    AgentSmithConfig config,
    ILogger<InheritedSandboxProjection> logger) : IInheritedSandboxProjection
{
    public InheritedSandboxSettings ProcessWide() => Without(new ResolvedProject());

    public IReadOnlyDictionary<string, InheritedSandboxSettings> ByProject() =>
        config.Projects.ToDictionary(kv => kv.Key, kv => Without(kv.Value), StringComparer.Ordinal);

    private InheritedSandboxSettings Without(ResolvedProject project)
    {
        var counterfactual = project with { Sandbox = null };
        return new InheritedSandboxSettings(
            ToolchainImage: resolver.ResolveToolchainImage(counterfactual),
            StepTimeoutSeconds: resolver.ResolveStepTimeout(counterfactual),
            RunCommandTimeoutSeconds: resolver.ResolveRunCommandTimeout(counterfactual),
            // The registry has no resolver of its own: AgentImageResolver composes it into a
            // whole image reference, and with an empty project block its layered lookup IS
            // the process-wide field. Reading the field is that answer, not a second one.
            AgentRegistry: ResolvedValue<string>.Global(global.Value.AgentRegistry),
            AgentVersion: InheritedVersion(counterfactual));
    }

    /// <summary>
    /// The tag an operator who pins nothing gets: the global pin, else the release this
    /// server is. The resolver throws on a hand-built binary that can name no release —
    /// this is a dashboard read, so that is reported as "nothing to inherit" rather than
    /// crashing the projection the way the run path deliberately still does.
    /// </summary>
    private ResolvedValue<string> InheritedVersion(ResolvedProject counterfactual)
    {
        try
        {
            return ResolvedValue<string>.Global(versions.Resolve(counterfactual).Version);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogDebug(ex, "No sandbox agent version can be derived — reporting none to inherit");
            return ResolvedValue<string>.Global(null!);
        }
    }
}
