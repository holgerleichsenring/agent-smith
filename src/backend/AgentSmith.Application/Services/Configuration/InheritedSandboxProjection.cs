using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Configuration.Resolved;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>
/// 2026-09-22-6968: runs the EXISTING resolution against a project whose sandbox block is
/// empty. The same code, one different input — writing a parallel "inheritance resolver"
/// would be a second answer able to disagree with the first, which is the defect the single
/// resolution pass was consolidated to end.
/// <para>
/// 2026-09-23-2446: the hold window is the ONE field read through the configuration loader
/// instead. Both <paramref name="global"/> and <paramref name="config"/> are assembled once
/// at composition and the config-epoch reloader refreshes neither, which is fine for every
/// other field here — their effects are frozen until a restart too. The hold window's is
/// not: a reaper reads it through the loader on every scan, so a placeholder built from the
/// composition-time options would disagree with the window in force from the moment an
/// operator edits anything, which is the only moment this control matters.
/// </para>
/// </summary>
public sealed class InheritedSandboxProjection(
    IConfigResolver resolver,
    IAgentVersionResolver versions,
    ISandboxResourceResolver resources,
    IOptions<SandboxGlobalConfig> global,
    AgentSmithConfig config,
    IConfigurationLoader loader,
    ILogger<InheritedSandboxProjection> logger) : IInheritedSandboxProjection
{
    public InheritedSandboxSettings ProcessWide() => Without(new ResolvedProject(), HoldWindow());

    public IReadOnlyDictionary<string, InheritedSandboxSettings> ByProject()
    {
        // ONE read for the whole projection: the loader re-assembles the entire catalog
        // from the document store on every call, and every row's counterfactual answer is
        // the same process-wide one — the per-project leg is exactly what is being emptied.
        var hold = HoldWindow();
        return config.Projects.ToDictionary(kv => kv.Key, kv => Without(kv.Value, hold), StringComparer.Ordinal);
    }

    /// <summary>
    /// The window the NEXT reaper scan will use, read the way that scan reads it. The
    /// process-wide sandbox block through the loader, then the environment variable, then
    /// the built-in three minutes — one chain, so the placeholder and the reaper cannot
    /// give different answers. A dashboard request, not a hot loop.
    /// </summary>
    private ResolvedValue<int> HoldWindow() =>
        SandboxHoldWindow.ProcessWide(loader.LoadConfig(string.Empty).Sandbox);

    private InheritedSandboxSettings Without(ResolvedProject project, ResolvedValue<int> hold)
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
            AgentVersion: InheritedVersion(counterfactual),
            HoldSeconds: hold,
            Resources: InheritedResources(counterfactual, project.Pipeline),
            Images: CodeDefaultImages);
    }

    /// <summary>
    /// The cpu/memory the project would be given with no resources of its own, and the
    /// LAYER that produced it — from the one resolver, in one walk, so the layer named can
    /// never belong to a different answer than the value shown. The project's CONFIGURED
    /// pipeline decides it, exactly as the effective snapshot's does; no context resources
    /// are passed, because that document is written per run and the layer says so.
    /// </summary>
    private InheritedSandboxResources InheritedResources(ResolvedProject counterfactual, string? pipeline) =>
        new(resources.Resolve(counterfactual, pipeline),
            resources.ResolveLayer(counterfactual, pipeline));

    /// <summary>
    /// The per-language table a project's image map is merged over, ONE ANSWER PER KEY —
    /// including keys the project has not named, which is the whole point: pinning
    /// <c>dotnet</c> leaves <c>node</c> inheriting. The source is the code table, not the
    /// configuration, and it is labelled as such so nobody goes looking for a setting.
    /// </summary>
    private static IReadOnlyDictionary<string, ResolvedValue<string>> CodeDefaultImages =>
        ToolchainImageCatalog.KnownLanguages.ToDictionary(
            kv => kv.Key, kv => ResolvedValue<string>.CodeDefault(kv.Value), StringComparer.OrdinalIgnoreCase);

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
