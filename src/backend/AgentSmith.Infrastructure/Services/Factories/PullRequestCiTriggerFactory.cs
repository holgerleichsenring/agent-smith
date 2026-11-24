using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Providers.Pipelines;
using AgentSmith.Infrastructure.Services.Providers.Source;

namespace AgentSmith.Infrastructure.Services.Factories;

/// <summary>
/// 2026-11-24-f9c5d: the pull-request CI trigger of a repository's host, authenticated with the repo's own auth secret as its
/// pull requests and pipelines are (<see cref="PipelineRunnerFactory"/>). A local working copy has none.
/// </summary>
public sealed class PullRequestCiTriggerFactory(
    ICredentialResolver credentials, IHttpClientFactory httpClients, IAzDoClientFactory azDo) : IPullRequestCiTriggerFactory
{
    public IPullRequestCiTrigger? Create(RepoConnection repo)
    {
        ArgumentNullException.ThrowIfNull(repo);
        if (string.IsNullOrWhiteSpace(repo.Url)) return null;
        var token = credentials.For(repo);
        return repo.Type switch
        {
            RepoType.GitHub => new GitHubCiTrigger(new PipelineHttp(httpClients.CreateClient(), r =>
            {
                r.Headers.Authorization = new("Bearer", token);
                r.Headers.Accept.ParseAdd("application/vnd.github+json");
                r.Headers.UserAgent.ParseAdd("agent-smith");
            }), GitHubRepoAddress.Parse(repo.Url)),
            RepoType.GitLab => GitLab(repo, token),
            RepoType.AzureDevOps => Azure(repo, token),
            _ => null,
        };
    }

    private GitLabCiTrigger GitLab(RepoConnection repo, string token)
    {
        var (baseUrl, projectPath, _) = SourceProviderFactory.ResolveGitLabTarget(repo.Url!, repo.Host);
        return new GitLabCiTrigger(new PipelineHttp(httpClients.CreateClient(), r => r.Headers.Add("PRIVATE-TOKEN", token)),
            $"{baseUrl}/api/v4/projects/{Uri.EscapeDataString(projectPath)}");
    }

    private AzureReposCiTrigger Azure(RepoConnection repo, string token)
    {
        var a = AzureDevOpsRepoAddress.Parse(repo.Url!);
        return new AzureReposCiTrigger(ct => azDo.CreateGitClientAsync(a.OrganizationUrl, token, ct),
            async (projectId, ct) => await (await azDo.CreatePolicyClientAsync(a.OrganizationUrl, token, ct)).GetPolicyConfigurationsAsync(
                projectId, policyType: Guid.Parse(AzureReposCiPolicies.BuildPolicyType), cancellationToken: ct),
            ct => azDo.CreateBuildClientAsync(a.OrganizationUrl, token, ct), a.Project, a.Repo, a.OrganizationUrl);
    }
}
