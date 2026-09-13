using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-13-84c0: the template entries a derivation may look into, built lazily and owned
/// by the look that gets them.
/// <para>
/// The binding is per CONTEXT (2026-09-13-5fa0), so a run selects the declarations whose
/// context it actually discovered — a project may declare a template for a component this
/// ticket never touches, and materialising that would be a clone nobody asked for.
/// </para>
/// </summary>
public sealed class DerivationTemplateScopes(
    ISourceScopeSandboxFactory scopes, ILogger<DerivationLook> logger)
{
    /// <summary>How a template entry is addressed, so a model can tell one from a target.</summary>
    public const string NamePrefix = "template:";

    public IReadOnlyDictionary<string, ISourceScopeSandbox> For(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<ResolvedProject>(ContextKeys.ProjectConfig, out var project)
            || project is null || project.Templates.Count == 0)
            return new Dictionary<string, ISourceScopeSandbox>(StringComparer.Ordinal);

        var discovered = DiscoveredContexts(pipeline);
        var result = new Dictionary<string, ISourceScopeSandbox>(StringComparer.Ordinal);
        foreach (var template in project.Templates)
        {
            if (discovered.Count > 0 && !discovered.Contains(template.Context)) continue;
            var name = $"{NamePrefix}{template.Context}";
            if (result.ContainsKey(name)) continue;
            result[name] = scopes.Create(project, template.Repo, template.Revision);
            logger.LogInformation(
                "The derivation may look into template '{Name}' ({Repo} at {Revision})",
                name, template.Repo.Name, template.Revision ?? "its own default");
        }
        return result;
    }

    /// <summary>
    /// Every context this run actually checked out. Empty when the run published none — a
    /// resume, or a preset that discovers nothing — and an empty set selects everything
    /// rather than nothing, because refusing to look is the worse failure of the two.
    /// </summary>
    private static HashSet<string> DiscoveredContexts(PipelineContext pipeline)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(
                ContextKeys.Sandboxes, out var sandboxes) || sandboxes is null)
            return names;
        foreach (var key in sandboxes.Keys)
            foreach (var context in SandboxContextList.In(pipeline, key))
                names.Add(context.ContextName);
        return names;
    }
}
