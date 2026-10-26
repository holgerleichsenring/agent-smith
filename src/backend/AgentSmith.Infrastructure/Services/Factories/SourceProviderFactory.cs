using AgentSmith.Infrastructure.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Providers.Source;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories;

/// <summary>
/// Creates the appropriate ISourceProvider based on configuration type. 2026-10-02-5f89a: the
/// token is the repo's own auth secret, and a GitLab repo's instance is its own host.
/// </summary>
public sealed class SourceProviderFactory(
    ICredentialResolver credentials,
    IHttpClientFactory httpClientFactory,
    IGitHubClientFactory gitHubClientFactory,
    IAzDoClientFactory azDoClientFactory,
    ILoggerFactory loggerFactory) : ISourceProviderFactory
{
    public ISourceProvider Create(RepoConnection config)
    {
        return config.Type switch
        {
            RepoType.Local => CreateLocal(config),
            RepoType.GitHub => CreateGitHub(config),
            RepoType.GitLab => CreateGitLab(config),
            RepoType.AzureDevOps => CreateAzureRepos(config),
            _ => throw new ConfigurationException($"Unknown source provider type: {config.Type}")
        };
    }

    private static LocalSourceProvider CreateLocal(RepoConnection config)
    {
        return new LocalSourceProvider(config.Path!, config.DefaultBranch);
    }

    private GitHubSourceProvider CreateGitHub(RepoConnection config)
    {
        var token = credentials.For(config);
        var connection = new GitHubSourceConnection(config.Url!, token, config.DefaultBranch);
        return new GitHubSourceProvider(
            connection, gitHubClientFactory,
            loggerFactory.CreateLogger<GitHubSourceProvider>());
    }

    private GitLabSourceProvider CreateGitLab(RepoConnection config)
    {
        var token = credentials.For(config);
        var (baseUrl, projectPath, cloneUrl) = ResolveGitLabTarget(config.Url!, config.Host);
        var connection = new GitLabSourceConnection(
            baseUrl, Uri.EscapeDataString(projectPath), cloneUrl, token, config.DefaultBranch);
        return new GitLabSourceProvider(
            connection,
            httpClientFactory.CreateClient(), loggerFactory.CreateLogger<GitLabSourceProvider>());
    }

    private AzureReposSourceProvider CreateAzureRepos(RepoConnection config)
    {
        var token = credentials.For(config);
        var address = AzureDevOpsRepoAddress.Parse(config.Url!);
        var connection = new AzureReposSourceConnection(
            address.OrganizationUrl, address.Project, address.Repo, token, config.DefaultBranch);
        return new AzureReposSourceProvider(
            connection,
            azDoClientFactory,
            loggerFactory.CreateLogger<AzureReposSourceProvider>());
    }

    /// <summary>
    /// The GitLab API base URL comes from the repo url's OWN host — the url is complete,
    /// so a self-managed instance needs no extra config. The repo's <c>Host</c> is only an
    /// optional override for a GitLab installed under a sub-path (host/gitlab/…), where the
    /// host is not the instance root and the path can't be split from it algorithmically.
    /// </summary>
    internal static (string BaseUrl, string ProjectPath, string CloneUrl) ResolveGitLabTarget(
        string repoUrl, string? gitlabUrlOverride)
    {
        var uri = new Uri(RepoKey.WithoutGitSuffix(repoUrl));
        var baseUrl = string.IsNullOrWhiteSpace(gitlabUrlOverride)
            ? $"{uri.Scheme}://{uri.Authority}"
            : gitlabUrlOverride.TrimEnd('/');
        var projectPath = uri.AbsolutePath.Trim('/');
        return (baseUrl, projectPath, $"{baseUrl}/{projectPath}.git");
    }
}
