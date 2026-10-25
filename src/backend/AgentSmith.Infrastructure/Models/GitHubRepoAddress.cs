using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Models;

/// <summary>
/// 2026-10-25-9dbbb: a GitHub repository as every GitHub caller addresses it — owner, name, its web
/// URL without a trailing ".git", and the API endpoints of the host the URL names. Only a TRAILING
/// ".git" is dropped, so a repository named acme.github.io keeps its name.
/// </summary>
public sealed record GitHubRepoAddress(string Owner, string Repo, string RepoUrl, GitHubApiEndpoints Api)
{
    private const string GitSuffix = ".git";

    public string FullName => $"{Owner}/{Repo}";

    public static GitHubRepoAddress Parse(string url) =>
        TryParse(url, out var address) ? address! : throw new ConfigurationException($"Invalid GitHub URL: {url}");

    public static bool TryParse(string? url, out GitHubRepoAddress? address)
    {
        address = null;
        if (string.IsNullOrWhiteSpace(url)) return false;
        var trimmed = url.Trim().TrimEnd('/');
        if (trimmed.EndsWith(GitSuffix, StringComparison.OrdinalIgnoreCase)) trimmed = trimmed[..^GitSuffix.Length];
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return false;
        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 2 || segments[0].Length == 0 || segments[1].Length == 0) return false;
        address = new GitHubRepoAddress(segments[0], segments[1], trimmed, GitHubApiEndpoints.For(trimmed));
        return true;
    }
}
