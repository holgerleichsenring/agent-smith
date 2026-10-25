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
/// spec is built after the first time a context list changed.
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
    /// ON — the spec's own <c>contexts</c> list. Empty or null selects every
    /// discovered context, which is what the derivation wants: it is deciding which
    /// contexts the spec touches, so it cannot be handed the answer.
    /// </param>
    public IReadOnlyDictionary<string, ISourceScopeSandbox> For(
        PipelineContext pipeline, IReadOnlyCollection<string>? contexts = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var declared = TemplateDeclarations.For(pipeline, contexts);
        return declared.Count == 0
            ? new Dictionary<string, ISourceScopeSandbox>(TemplateScopeName.Comparer)
            : Open(pipeline.Get<ResolvedProject>(ContextKeys.ProjectConfig), declared);
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
        return Open(project, TemplateDeclarations.Of(project, [], contexts: null), conversationId);
    }

    /// <summary>2026-10-25-54beb: one declaration's scope, created and not yet materialised.</summary>
    public ISourceScopeSandbox Open(
        ResolvedProject project, string name, ProjectTemplate template, string? conversationId = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(template);
        logger.LogInformation(
            "Template '{Name}' ({Repo} at {Revision}) is open to this run",
            name, template.Repo.Name, template.Revision ?? "its own default");
        return scopes.Create(project, template.Repo, template.Revision, conversationId);
    }

    private Dictionary<string, ISourceScopeSandbox> Open(
        ResolvedProject project, IReadOnlyDictionary<string, ProjectTemplate> declared,
        string? conversationId = null) =>
        declared.ToDictionary(
            d => d.Key, d => Open(project, d.Key, d.Value, conversationId), TemplateScopeName.Comparer);
}
