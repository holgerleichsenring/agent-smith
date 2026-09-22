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
    /// <param name="conversationId">
    /// 2026-09-22-2d11b: null for a run, and the design conversation's id for a dialog turn —
    /// which is what lets the sandbox a template is read through outlive the turn that opened it.
    /// </param>
    public IReadOnlyDictionary<string, ISourceScopeSandbox> ForProject(
        ResolvedProject project, string? conversationId = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        return Select(project, [], contexts: null, conversationId);
    }

    private IReadOnlyDictionary<string, ISourceScopeSandbox> Select(
        ResolvedProject project, HashSet<string> discovered, IReadOnlyCollection<string>? contexts,
        string? conversationId = null)
    {
        if (project.Templates.Count == 0) return Empty();
        var wanted = contexts is null || contexts.Count == 0
            ? null
            : new HashSet<string>(contexts, StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, ISourceScopeSandbox>(TemplateScopeName.Comparer);
        for (var ordinal = 0; ordinal < project.Templates.Count; ordinal++)
        {
            var template = project.Templates[ordinal];
            if (!Admits(template, discovered, wanted)) continue;
            // 2026-09-15-6f8d: one address is opened once, by the declaration that OWNS it —
            // the statement the proof report reads too, over this same full list. Deciding it
            // here by "is this key already taken" would be a second copy of the precedence
            // rule, and it read the contexts case-insensitively while keying the result
            // ordinally: two spellings of one context passed one filter check as one context
            // and then opened two clones of one repository.
            if (!TemplateScopeName.Owns(project.Templates, ordinal)) continue;
            var name = TemplateScopeName.For(template);
            result[name] = scopes.Create(project, template.Repo, template.Revision, conversationId);
            logger.LogInformation(
                "Template '{Name}' ({Repo} at {Revision}) is open to this run",
                name, template.Repo.Name, template.Revision ?? "its own default");
        }
        return result;
    }

    // Whether this run is working on the context the declaration binds — what was checked out,
    // and what the caller narrowed to. Both fold case, as the address comparison does.
    private static bool Admits(
        ProjectTemplate template, HashSet<string> discovered, HashSet<string>? wanted) =>
        (discovered.Count == 0 || discovered.Contains(template.Context))
        && (wanted is null || wanted.Contains(template.Context));

    private static Dictionary<string, ISourceScopeSandbox> Empty() =>
        new(TemplateScopeName.Comparer);

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
