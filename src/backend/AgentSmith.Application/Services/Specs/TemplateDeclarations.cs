using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-25-54beb: which declarations a run's template selection admits, keyed by the address
/// each one OWNS — read without opening a scope, so a run-scoped owner can hand back the scope it
/// already holds for an address instead of cloning the template again. Split out of
/// <see cref="ProjectTemplateScopes"/>, which opens what this selects.
/// </summary>
public static class TemplateDeclarations
{
    /// <summary>The run's selection, narrowed to <paramref name="contexts"/> when given; empty
    /// when the pipeline carries no project.</summary>
    public static IReadOnlyDictionary<string, ProjectTemplate> For(
        PipelineContext pipeline, IReadOnlyCollection<string>? contexts = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<ResolvedProject>(ContextKeys.ProjectConfig, out var project) && project is not null
            ? Of(project, DiscoveredContexts(pipeline), contexts)
            : new Dictionary<string, ProjectTemplate>(TemplateScopeName.Comparer);
    }

    /// <summary>The declarations admitted from <paramref name="project"/>; an empty
    /// <paramref name="discovered"/> set admits every context.</summary>
    public static IReadOnlyDictionary<string, ProjectTemplate> Of(
        ResolvedProject project, HashSet<string> discovered, IReadOnlyCollection<string>? contexts)
    {
        ArgumentNullException.ThrowIfNull(project);
        var result = new Dictionary<string, ProjectTemplate>(TemplateScopeName.Comparer);
        var wanted = contexts is null || contexts.Count == 0
            ? null
            : new HashSet<string>(contexts, StringComparer.OrdinalIgnoreCase);
        for (var ordinal = 0; ordinal < project.Templates.Count; ordinal++)
        {
            var template = project.Templates[ordinal];
            if (!Admits(template, discovered, wanted)) continue;
            // 2026-09-15-6f8d: one address is opened once, by the declaration that OWNS it —
            // the statement the proof report reads too, over this same full list. Deciding it
            // by "is this key already taken" would be a second copy of the precedence rule.
            if (!TemplateScopeName.Owns(project.Templates, ordinal)) continue;
            result[TemplateScopeName.For(template)] = template;
        }
        return result;
    }

    // Whether this run is working on the context the declaration binds — what was checked out,
    // and what the caller narrowed to. Both fold case, as the address comparison does.
    private static bool Admits(
        ProjectTemplate template, HashSet<string> discovered, HashSet<string>? wanted) =>
        (discovered.Count == 0 || discovered.Contains(template.Context))
        && (wanted is null || wanted.Contains(template.Context));

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
