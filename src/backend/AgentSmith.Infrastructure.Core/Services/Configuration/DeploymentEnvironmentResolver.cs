using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// 2026-11-18-3863a: resolves a deployment binding's environments. An environment's repo is a ref from the REPOS
/// CATALOG, resolved the way a template's own repo is — not one of this project's repos, so a GitOps repository is
/// never checked out by the project's coding runs. A ref that does not resolve is a blocking finding on the project.
/// </summary>
public sealed class DeploymentEnvironmentResolver(ProjectRepoResolver repoResolver)
{
    public IReadOnlyList<DeploymentEnvironment>? Resolve(
        string project, RawTemplateEntry raw, ConfigCatalogs catalogs, List<StartupFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var resolved = new List<DeploymentEnvironment>();
        foreach (var environment in raw.Environments ?? [])
        {
            var repo = ResolveRepo(project, raw, environment, catalogs, findings);
            if (repo is null && !string.IsNullOrWhiteSpace(environment.Repo)) return null;
            resolved.Add(new DeploymentEnvironment(environment.Name, repo, environment.Approval ?? true,
                new Dictionary<string, string>(environment.Parameters ?? [], ConfigNames.Comparer)));
        }
        return resolved;
    }

    private RepoConnection? ResolveRepo(
        string project, RawTemplateEntry raw, RawDeploymentEnvironment environment,
        ConfigCatalogs catalogs, List<StartupFinding> findings)
    {
        if (string.IsNullOrWhiteSpace(environment.Repo)) return null;
        var repos = repoResolver.Resolve(
            project, [new RawRepoRef(environment.Repo)], catalogs.Repos, catalogs.Connections, globExpander: null, findings);
        if (repos is { Count: > 0 }) return repos[0];
        findings.Add(ProjectFindings.Blocking(project, "templates",
            $"project '{project}': template for context '{raw.Context}': environment '{environment.Name}' names repo ref "
            + $"'{environment.Repo}', which does not resolve to a repository"));
        return null;
    }
}
