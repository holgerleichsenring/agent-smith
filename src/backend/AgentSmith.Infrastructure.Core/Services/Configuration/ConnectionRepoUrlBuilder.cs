using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0285: deterministic repo-URL builder for EXACT connection repo refs. Produces the same
/// clone URL the discovery providers return (ADO: dev.azure.com/{org}/{project}/_git/{name};
/// GitHub: github.com/{owner}/{name}; GitLab: {host|gitlab.com}/{group}/{name}) with no API call.
/// </summary>
public sealed class ConnectionRepoUrlBuilder : IConnectionRepoUrlBuilder
{
    private const string AzureDevOpsDefaultHost = "https://dev.azure.com";
    private const string GitHubDefaultHost = "https://github.com";
    private const string GitLabDefaultHost = "https://gitlab.com";

    public RepoConnection Build(ResolvedConnection conn, string repoName, string? branchOverride) =>
        new()
        {
            Name = repoName,
            Type = conn.Type,
            Url = BuildUrl(conn, repoName),
            Organization = conn.Organization,
            Project = conn.Project,
            Auth = conn.Auth,
            DefaultBranch = branchOverride ?? conn.DefaultBranch,
        };

    private static string BuildUrl(ResolvedConnection conn, string repoName) => conn.Type switch
    {
        RepoType.AzureDevOps => BuildAzureDevOpsUrl(conn, repoName),
        RepoType.GitHub => BuildGitHubUrl(conn, repoName),
        RepoType.GitLab => BuildGitLabUrl(conn, repoName),
        _ => throw new ConfigurationException(
            $"Connection '{conn.Name}': type '{conn.Type}' is not a supported connection host for static repo refs."),
    };

    private static string BuildAzureDevOpsUrl(ResolvedConnection conn, string repoName)
    {
        RequireField(conn, conn.Organization, "organization");
        RequireField(conn, conn.Project, "project");
        var host = Host(conn.Host, AzureDevOpsDefaultHost);
        return $"{host}/{conn.Organization}/{Uri.EscapeDataString(conn.Project!)}/_git/{repoName}";
    }

    private static string BuildGitHubUrl(ResolvedConnection conn, string repoName)
    {
        RequireField(conn, conn.Owner, "owner");
        return $"{Host(conn.Host, GitHubDefaultHost)}/{conn.Owner}/{repoName}";
    }

    private static string BuildGitLabUrl(ResolvedConnection conn, string repoName)
    {
        RequireField(conn, conn.Group, "group");
        return $"{Host(conn.Host, GitLabDefaultHost)}/{conn.Group}/{repoName}";
    }

    private static string Host(string? host, string fallback) =>
        string.IsNullOrEmpty(host) ? fallback : host.TrimEnd('/');

    private static void RequireField(ResolvedConnection conn, string? value, string field)
    {
        if (string.IsNullOrEmpty(value))
            throw new ConfigurationException(
                $"Connection '{conn.Name}' ({conn.Type}) requires '{field}' to resolve a static repo reference.");
    }
}
