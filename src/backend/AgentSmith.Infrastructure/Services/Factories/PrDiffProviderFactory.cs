using System.Net.Http.Headers;
using System.Text;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Providers.Source;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories;

/// <summary>
/// p0167a: creates the platform-appropriate IPrDiffProvider from a repo
/// connection, resolving tokens the same way SourceProviderFactory does — 2026-10-02-5f89a:
/// through the repo's own auth secret.
/// </summary>
public sealed class PrDiffProviderFactory(
    ICredentialResolver credentials,
    IHttpClientFactory httpClientFactory,
    IGitHubClientFactory gitHubClientFactory,
    ILoggerFactory loggerFactory) : IPrDiffProviderFactory
{
    public IPrDiffProvider Create(RepoConnection repo) => repo.Type switch
    {
        RepoType.GitHub => CreateGitHub(repo),
        RepoType.GitLab => CreateGitLab(repo),
        RepoType.AzureDevOps => CreateAzureDevOps(repo),
        _ => throw new ConfigurationException(
            $"Repo '{repo.Name}' has type '{repo.Type}' which has no pull requests — " +
            "pr-review requires a GitHub / GitLab / Azure DevOps repo."),
    };

    private GitHubPrDiffProvider CreateGitHub(RepoConnection repo)
    {
        var token = credentials.For(repo);
        return new GitHubPrDiffProvider(
            gitHubClientFactory.Create(GitHubRepoAddress.Parse(repo.Url!), token),
            new GitHubTicketConnection(repo.Url!, token),
            loggerFactory.CreateLogger<GitHubPrDiffProvider>());
    }

    private GitLabPrDiffProvider CreateGitLab(RepoConnection repo)
    {
        var token = credentials.For(repo);
        var (baseUrl, projectPath, _) = SourceProviderFactory.ResolveGitLabTarget(repo.Url!, repo.Host);
        var httpClient = httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri($"{baseUrl}/api/v4/");
        httpClient.DefaultRequestHeaders.Add("PRIVATE-TOKEN", token);
        return new GitLabPrDiffProvider(
            httpClient, projectPath, loggerFactory.CreateLogger<GitLabPrDiffProvider>());
    }

    private AzureDevOpsPrDiffProvider CreateAzureDevOps(RepoConnection repo)
    {
        var token = credentials.For(repo);
        var address = AzureDevOpsRepoAddress.Parse(repo.Url!);
        var httpClient = httpClientFactory.CreateClient();
        // The trailing slash keeps the organization (or collection) path when the provider's relative paths resolve.
        httpClient.BaseAddress = new Uri($"{address.OrganizationUrl}/");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{token}")));
        return new AzureDevOpsPrDiffProvider(
            httpClient, address.Project, address.Repo,
            loggerFactory.CreateLogger<AzureDevOpsPrDiffProvider>());
    }
}
