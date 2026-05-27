using AgentSmith.Infrastructure.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Services.Providers.Source;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories;

/// <summary>
/// Creates the appropriate ISourceProvider based on configuration type.
/// </summary>
public sealed class SourceProviderFactory(
    SecretsProvider secrets,
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
        return new LocalSourceProvider(config.Path!);
    }

    private GitHubSourceProvider CreateGitHub(RepoConnection config)
    {
        var token = secrets.GetRequired("GITHUB_TOKEN");
        var connection = new GitHubSourceConnection(config.Url!, token, config.DefaultBranch);
        return new GitHubSourceProvider(
            connection, gitHubClientFactory,
            loggerFactory.CreateLogger<GitHubSourceProvider>());
    }

    private GitLabSourceProvider CreateGitLab(RepoConnection config)
    {
        var token = secrets.GetRequired("GITLAB_TOKEN");
        var baseUrl = secrets.GetOptional("GITLAB_URL") ?? AgentDefaults.DefaultGitLabBaseUrl;
        var projectPath = ExtractGitLabProjectPath(config.Url!);
        var cloneUrl = $"{baseUrl}/{projectPath}.git";
        var connection = new GitLabSourceConnection(
            baseUrl, Uri.EscapeDataString(projectPath), cloneUrl, token, config.DefaultBranch);
        return new GitLabSourceProvider(
            connection,
            httpClientFactory.CreateClient(), loggerFactory.CreateLogger<GitLabSourceProvider>());
    }

    private AzureReposSourceProvider CreateAzureRepos(RepoConnection config)
    {
        var token = secrets.GetRequired("AZURE_DEVOPS_TOKEN");
        var (orgUrl, project, repoName) = ParseAzureReposUrl(config.Url!);
        var connection = new AzureReposSourceConnection(orgUrl, project, repoName, token);
        return new AzureReposSourceProvider(
            connection,
            azDoClientFactory,
            loggerFactory.CreateLogger<AzureReposSourceProvider>());
    }

    private static string ExtractGitLabProjectPath(string url)
    {
        var uri = new Uri(url.Replace(".git", ""));
        return uri.AbsolutePath.Trim('/');
    }

    private static (string orgUrl, string project, string repoName) ParseAzureReposUrl(string url)
    {
        // https://dev.azure.com/{org}/{project}/_git/{repo}
        var uri = new Uri(url.Replace(".git", ""));
        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 4 || segments[2] != "_git")
            throw new ConfigurationException($"Invalid Azure Repos URL: {url}");

        var orgUrl = $"{uri.Scheme}://{uri.Host}/{segments[0]}";
        return (orgUrl, segments[1], segments[3]);
    }
}
