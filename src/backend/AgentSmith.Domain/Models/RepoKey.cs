namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-10-08-10b0: a repository named the way every caller compares it — host and path, lowercased,
/// no scheme, user info or .git suffix — so a payload's clone URL and the configured web URL are one
/// key. Extracted from ConfiguredRepoFinder for the PR sweep's rows.
/// <para>2026-10-25-9dbbg: in Domain so configuration findings share it; only a TRAILING ".git" is
/// a suffix, so a repository named my.gitops or app.github.io keeps its name.</para>
/// </summary>
public static class RepoKey
{
    private const string GitSuffix = ".git";

    public static string Of(string url)
    {
        var normalized = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? $"{uri.Host}{uri.AbsolutePath}" : url;
        return WithoutGitSuffix(normalized).ToLowerInvariant();
    }

    /// <summary>The url without trailing slashes and without a trailing ".git"; nothing inside it changes.</summary>
    public static string WithoutGitSuffix(string url)
    {
        var trimmed = url.TrimEnd('/');
        return trimmed.EndsWith(GitSuffix, StringComparison.OrdinalIgnoreCase) ? trimmed[..^GitSuffix.Length] : trimmed;
    }
}
