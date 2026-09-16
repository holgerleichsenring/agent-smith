using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// 2026-09-13-5fa0: the raw-to-editable projection of one project, carved out of
/// <see cref="ConfigCatalogMapper"/> — which sat exactly at its file-length baseline and
/// so could not take the templates list without giving something up first.
/// </summary>
internal static class ProjectEntityMapping
{
    public static ProjectEntity ToProject(string id, RawProjectEntry project)
    {
        var pipelines = project.Pipelines.Count > 0
            ? project.Pipelines.Select(p => p.Name).ToList()
            : !string.IsNullOrWhiteSpace(project.Pipeline) ? [project.Pipeline] : new List<string>();
        return new ProjectEntity(
            id,
            project.Agent,
            project.Tracker,
            project.Repos.Select(r => r.Ref).ToList(),
            string.IsNullOrWhiteSpace(project.Pipeline) ? null : project.Pipeline,
            pipelines,
            ToResolution(project),
            project.DefaultPipeline,
            ToTemplates(project));
    }

    /// <summary>
    /// Always a list, never null, on the way OUT: the studio is being told what the stored
    /// project holds, and "none" is an answer. Null on the way IN means something else
    /// entirely — see ProjectEntity.Templates.
    /// </summary>
    public static IReadOnlyList<TemplateReference> ToTemplates(RawProjectEntry project) =>
        [.. project.Templates.Select(t => new TemplateReference(
            t.Context, t.Project, t.Repo, t.TemplateContext, t.Revision, t.ContextRepo))];

    public static List<RawTemplateEntry> ToRaw(IReadOnlyList<TemplateReference> templates) =>
        [.. templates.Select(t => new RawTemplateEntry
        {
            Context = t.Context,
            Project = t.Project,
            Repo = t.Repo,
            TemplateContext = t.TemplateContext,
            Revision = t.Revision,
            ContextRepo = t.ContextRepo,
        })];

    // p0345c: surface the flat resolution shorthand; when the project instead
    // declares a full trigger wrapper, surface ITS resolution read-only so the
    // studio shows how the project actually routes either way.
    private static ProjectResolution? ToResolution(RawProjectEntry project)
    {
        if (project.Resolution is { Count: > 0 } shorthand)
        {
            var first = shorthand.First();
            return new ProjectResolution(first.Key, first.Value);
        }
        var wrapperResolution = new WebhookTriggerConfig?[]
            {
                project.JiraTrigger, project.GithubTrigger,
                project.GitlabTrigger, project.AzuredevopsTrigger,
            }
            .FirstOrDefault(t => t?.ProjectResolution is not null)?.ProjectResolution;
        return wrapperResolution is null
            ? null
            : new ProjectResolution(
                Contracts.Services.ConfigStudioCapabilities.WireName(wrapperResolution.Strategy),
                wrapperResolution.Value);
    }
}
