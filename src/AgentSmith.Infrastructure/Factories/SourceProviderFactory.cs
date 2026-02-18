using AgentSmith.Contracts.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Configuration;
using AgentSmith.Infrastructure.Providers.Source;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Factories;

/// <summary>
/// Creates the appropriate ISourceProvider based on configuration type.
/// </summary>
public sealed class SourceProviderFactory(
    SecretsProvider secrets,
    ILoggerFactory loggerFactory) : ISourceProviderFactory
{
    public ISourceProvider Create(SourceConfig config)
    {
        return config.Type.ToLowerInvariant() switch
        {
            "local" => CreateLocal(config),
            "github" => CreateGitHub(config),
            "gitlab" => throw new NotSupportedException("GitLab provider not yet implemented."),
            "azurerepos" => throw new NotSupportedException("Azure Repos provider not yet implemented."),
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
}
