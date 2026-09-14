using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-13-84c0: the template entries a look may open, built lazily and owned by the
/// look that gets them.
/// <para>
/// 2026-09-13-6f35: it was <c>DerivationTemplateScopes</c> while the derivation was the
/// only look. The coding master opens the same templates from the same declarations, and
/// one selection serving both is the point — two would disagree about which template a
/// phase is built after the first time a context list changed.
/// </para>
/// <para>
/// The binding is per CONTEXT (2026-09-13-5fa0), so a run selects the declarations whose
/// context it actually discovered — a project may declare a template for a component this
/// ticket never touches, and materialising that would be a clone nobody asked for.
/// </para>
/// </summary>
public sealed class ProjectTemplateScopes(
    ISourceScopeSandboxFactory scopes, ILogger<ProjectTemplateScopes> logger)
{
    /// <summary>How a template entry is addressed, so a model can tell one from a target.</summary>
    public const string NamePrefix = "template:";

    /// <param name="contexts">
    /// 2026-09-13-6f35: narrows the selection further to the contexts a caller is working
    /// ON — the phase spec's own <c>contexts</c> list. Empty or null selects every
    /// discovered context, which is what the derivation wants: it is deciding which
    /// contexts the phase touches, so it cannot be handed the answer.
    /// </param>
    public IReadOnlyDictionary<string, ISourceScopeSandbox> For(
        PipelineContext pipeline, IReadOnlyCollection<string>? contexts = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<ResolvedProject>(ContextKeys.ProjectConfig, out var project)
            || project is null)
            return Empty();
        return Select(project, DiscoveredContexts(pipeline), contexts);
    }

    /// <summary>
    /// 2026-09-13-ed5a: EVERY template the project declares, for the look that has no
    /// contexts to select by. The spec dialog's epic analysis is deciding what to build
    /// before anything is checked out, so it has discovered nothing and is handed nothing —
    /// and a cut made without the house shape is inherited by every run the epic files.
    /// </summary>
    public IReadOnlyDictionary<string, ISourceScopeSandbox> ForProject(ResolvedProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return Select(project, [], contexts: null);
    }

    private IReadOnlyDictionary<string, ISourceScopeSandbox> Select(
        ResolvedProject project, HashSet<string> discovered, IReadOnlyCollection<string>? contexts)
    {
        if (project.Templates.Count == 0) return Empty();
        var wanted = contexts is null || contexts.Count == 0
            ? null
            : new HashSet<string>(contexts, StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, ISourceScopeSandbox>(StringComparer.Ordinal);
        foreach (var template in project.Templates)
        {
            if (discovered.Count > 0 && !discovered.Contains(template.Context)) continue;
            if (wanted is not null && !wanted.Contains(template.Context)) continue;
            var name = $"{NamePrefix}{template.Context}";
            if (result.ContainsKey(name)) continue;
            result[name] = scopes.Create(project, template.Repo, template.Revision);
            logger.LogInformation(
                "Template '{Name}' ({Repo} at {Revision}) is open to this run",
                name, template.Repo.Name, template.Revision ?? "its own default");
        }
        return result;
    }

    private static Dictionary<string, ISourceScopeSandbox> Empty() => new(StringComparer.Ordinal);

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
