using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// 2026-09-13-5fa0: turns a project's declared templates into resolved
/// <see cref="ProjectTemplate"/>s, carrying the TARGET's RepoConnection so a consumer
/// need not re-read the catalog mid-run.
/// <para>
/// Carved out rather than grown into <see cref="ResolvedProjectBuilder"/>, which sat at
/// its file-length baseline — the same reason <see cref="ProjectRepoResolver"/> was carved
/// out before it.
/// </para>
/// <para>
/// The four rules run HERE, in the loader, because the loader is the only path that can
/// author a template today: the studio has no picker for it, and neither import path calls
/// the referential validator. A rule that only fired on export would refuse after the
/// broken config was already stored.
/// </para>
/// </summary>
public sealed class ProjectTemplateResolver(ProjectRepoResolver repoResolver)
{
    public IReadOnlyList<ProjectTemplate> Resolve(
        string project,
        IReadOnlyList<RawTemplateEntry> raws,
        IReadOnlyDictionary<string, RawProjectEntry> rawProjects,
        ConfigCatalogs catalogs,
        List<StartupFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(raws);
        ArgumentNullException.ThrowIfNull(rawProjects);
        if (raws.Count == 0) return [];

        var references = raws
            .Select(r => new TemplateReference(
                r.Context, r.Project, r.Repo, r.TemplateContext, r.Revision, r.ContextRepo))
            .ToList();
        var repoRefsByProject = rawProjects.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<string>)[.. kv.Value.Repos.Select(r => r.Ref)],
            ConfigNames.Comparer);

        var broken = ProjectTemplateRules.Check(project, references, repoRefsByProject);
        foreach (var message in broken)
            findings.Add(ProjectFindings.Blocking(project, "templates", message));
        if (broken.Count > 0) return [];

        return [.. raws
            .Select(raw => Materialise(project, raw, rawProjects, catalogs, findings))
            .Where(template => template is not null)
            .Select(template => template!)];
    }

    private ProjectTemplate? Materialise(
        string project, RawTemplateEntry raw,
        IReadOnlyDictionary<string, RawProjectEntry> rawProjects,
        ConfigCatalogs catalogs, List<StartupFinding> findings)
    {
        var target = rawProjects.First(kv => ConfigNames.AreSame(kv.Key, raw.Project));
        var repos = repoResolver.Resolve(
            target.Key, [new RawRepoRef(raw.Repo)], catalogs.Repos, catalogs.Connections,
            globExpander: null, findings);
        if (repos is not { Count: > 0 })
        {
            findings.Add(ProjectFindings.Blocking(project, "templates",
                $"project '{project}': template for context '{raw.Context}' names repo ref "
                + $"'{raw.Repo}' of project '{raw.Project}', which does not resolve to a repository"));
            return null;
        }
        return new ProjectTemplate(
            raw.Context, raw.TemplateContext, raw.Revision, repos[0], raw.ContextRepo);
    }
}
