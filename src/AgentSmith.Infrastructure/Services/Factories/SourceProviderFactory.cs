using AgentSmith.Infrastructure.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Services.Configuration;
using AgentSmith.Infrastructure.Services.Providers.Source;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories;

/// <summary>
/// Creates the appropriate ISourceProvider based on configuration type.
/// </summary>
public sealed class SourceProviderFactory(
    SecretsProvider secrets,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory) : ISourceProviderFactory
{
    public ISourceProvider Create(SourceConfig config)
    {
        return config.Type.ToLowerInvariant() switch
        {
            "local" => CreateLocal(config),
            "github" => CreateGitHub(config),
            "gitlab" => CreateGitLab(config),
            "azurerepos" => CreateAzureRepos(config),
            _ => throw new ConfigurationException($"Unknown source provider type: {config.Type}")
        };
    }

    private static LocalSourceProvider CreateLocal(SourceConfig config)
    {
        return new LocalSourceProvider(config.Path!);
    }

    private GitHubSourceProvider CreateGitHub(SourceConfig config)
    {
        var token = secrets.GetRequired("GITHUB_TOKEN");
        return new GitHubSourceProvider(
            config.Url!, token, loggerFactory.CreateLogger<GitHubSourceProvider>());
    }

    private GitLabSourceProvider CreateGitLab(SourceConfig config)
    {
        var token = secrets.GetRequired("GITLAB_TOKEN");
        var baseUrl = secrets.GetOptional("GITLAB_URL") ?? AgentDefaults.DefaultGitLabBaseUrl;
        var projectPath = ExtractGitLabProjectPath(config.Url!);
        var cloneUrl = $"{baseUrl}/{projectPath}.git";
        return new GitLabSourceProvider(
            baseUrl, Uri.EscapeDataString(projectPath), cloneUrl, token,
            httpClientFactory.CreateClient(), loggerFactory.CreateLogger<GitLabSourceProvider>());
    }

    private AzureReposSourceProvider CreateAzureRepos(SourceConfig config)
    {
        var token = secrets.GetRequired("AZURE_DEVOPS_TOKEN");
        var (orgUrl, project, repoName) = ParseAzureReposUrl(config.Url!);
        return new AzureReposSourceProvider(
            orgUrl, project, repoName, token,
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
