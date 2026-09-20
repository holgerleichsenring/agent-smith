using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-15-cb3e: what a spec dialog on a given project is grounded in — its
/// repositories and the templates its turns may read — read off the configuration the
/// turn runner itself reads.
/// <para>
/// A session row stores the project and the repo set it was opened on and no template at
/// all, so the templates are looked up here rather than carried: what the next turn may
/// read is what the configuration says TODAY, and a surface claiming otherwise would be
/// describing a turn that already ran.
/// </para>
/// </summary>
public sealed class SpecDialogProjectCatalog(IConfigurationLoader configLoader)
{
    /// <summary>Every project a new conversation can be opened on.</summary>
    public IReadOnlyList<SpecDialogProjectView> All() =>
        [.. Projects().Select(entry => new SpecDialogProjectView(
            entry.Key, [.. entry.Value.Repos.Select(repo => repo.Name)], Templates(entry.Value)))];

    /// <summary>
    /// The scope of an OPEN session: the repositories are the ones the session was opened
    /// on (the catalog may have moved since), the templates are the project's current
    /// declaration, and a project no longer configured resolves to no templates rather
    /// than to an error — the conversation still exists and still says what it was about.
    /// </summary>
    public SpecDialogProjectView Of(string project, IReadOnlyList<string> repos) =>
        new(project, repos,
            Projects().TryGetValue(project, out var resolved) ? Templates(resolved) : []);

    private IReadOnlyDictionary<string, ResolvedProject> Projects() =>
        configLoader.LoadConfig(DispatcherDefaults.ConfigPath).Projects;

    /// <summary>
    /// 2026-09-15-6f8d: one row per ADDRESS, not per declaration — a run opens one scope per
    /// address, so listing a duplicated declaration twice offers a template that cannot be
    /// opened a second time, and the panel collides with itself on the row key. The owner is
    /// picked by the one ownership statement, here as everywhere; the editor's form still
    /// shows every declaration, because what can be deleted is a different question from what
    /// a run can read.
    /// </summary>
    private static IReadOnlyList<SpecDialogTemplateView> Templates(ResolvedProject project)
    {
        var views = new List<SpecDialogTemplateView>();
        for (var ordinal = 0; ordinal < project.Templates.Count; ordinal++)
        {
            if (!TemplateScopeName.Owns(project.Templates, ordinal)) continue;
            var template = project.Templates[ordinal];
            views.Add(new SpecDialogTemplateView(
                TemplateScopeName.For(template), template.Repo.Name,
                template.Revision ?? string.Empty));
        }
        return views;
    }
}
