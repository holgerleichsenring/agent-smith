using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories;

/// <summary>
/// Creates the platform-specific ITicketStatusTransitioner. p95a ships GitHub only;
/// GitLab, AzureDevOps, and Jira impls land in p95b.
/// </summary>
public sealed class TicketStatusTransitionerFactory(
    SecretsProvider secrets,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory) : ITicketStatusTransitionerFactory
{
    public ITicketStatusTransitioner Create(TicketConfig config)
        => config.Type.ToLowerInvariant() switch
        {
            "github" => CreateGitHub(config),
            _ => throw new NotSupportedException(
                $"ITicketStatusTransitioner for platform '{config.Type}' is not yet implemented (expected in p95b)")
        };

    private GitHubTicketStatusTransitioner CreateGitHub(TicketConfig config)
    {
        var token = secrets.GetRequired("GITHUB_TOKEN");
        return new GitHubTicketStatusTransitioner(
            config.Url ?? throw new ArgumentException("GitHub URL required"),
            token,
            httpClientFactory.CreateClient(),
            loggerFactory.CreateLogger<GitHubTicketStatusTransitioner>());
    }
}
