namespace AgentSmith.Infrastructure.Services.Webhooks;

/// <summary>
/// 2026-10-08-10b0: a repository named the way every caller compares it — host and path, lowercased,
/// no scheme, user info or .git suffix — so a payload's clone URL and the configured web URL are one
/// key. Extracted from <see cref="ConfiguredRepoFinder"/> for the PR sweep's rows.
/// </summary>
public static class RepoKey
{
    public static string Of(string url)
    {
        var normalized = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? $"{uri.Host}{uri.AbsolutePath}" : url;
        normalized = normalized.TrimEnd('/');
        if (normalized.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) normalized = normalized[..^4];
        return normalized.ToLowerInvariant();
    }
}
