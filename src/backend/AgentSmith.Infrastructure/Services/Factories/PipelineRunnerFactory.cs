using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Providers.Pipelines;
using AgentSmith.Infrastructure.Services.Providers.Source;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories;

/// <summary>
/// 2026-11-18-3863e: the pipeline runner of a repository's host, authenticated with the repo's own auth secret — the
/// token its pull requests are opened with, so it needs to run pipelines too. A local working copy runs none.
/// </summary>
public sealed class PipelineRunnerFactory(
    ICredentialResolver credentials, IHttpClientFactory httpClients, IAzDoClientFactory azDo,
    ILogger<PipelineRunnerFactory> logger) : IPipelineRunnerFactory
{
    public IPipelineRunner? Create(RepoConnection repo)
    {
        ArgumentNullException.ThrowIfNull(repo);
        logger.LogDebug("Pipeline runner for {Repo} ({Type})", repo.Name, repo.Type);
        return repo.Type switch
        {
            RepoType.GitHub => GitHub(repo),
            RepoType.GitLab => GitLab(repo),
            RepoType.AzureDevOps => Azure(repo),
            _ => null,
        };
    }

    private GitHubPipelineRunner GitHub(RepoConnection repo)
    {
        var token = credentials.For(repo);
        return new GitHubPipelineRunner(new PipelineHttp(httpClients.CreateClient(), r =>
        {
            r.Headers.Authorization = new("Bearer", token);
            r.Headers.Accept.ParseAdd("application/vnd.github+json");
            r.Headers.UserAgent.ParseAdd("agent-smith");
        }), GitHubRepoAddress.Parse(repo.Url!), repo.DefaultBranch);
    }

    private GitLabPipelineRunner GitLab(RepoConnection repo)
    {
        var token = credentials.For(repo);
        var (baseUrl, projectPath, _) = SourceProviderFactory.ResolveGitLabTarget(repo.Url!, repo.Host);
        return new GitLabPipelineRunner(new PipelineHttp(httpClients.CreateClient(), r => r.Headers.Add("PRIVATE-TOKEN", token)),
            $"{baseUrl}/api/v4/projects/{Uri.EscapeDataString(projectPath)}", repo.DefaultBranch);
    }

    private AzurePipelineRunner Azure(RepoConnection repo)
    {
        var token = credentials.For(repo);
        var address = AzureDevOpsRepoAddress.Parse(repo.Url!);
        return new AzurePipelineRunner(ct => azDo.CreateBuildClientAsync(address.OrganizationUrl, token, ct),
            address.Project, address.OrganizationUrl);
    }
}
