namespace AgentSmith.Infrastructure.Models;

/// <summary>
/// 2026-10-25-9dbbb: the REST base and the GraphQL endpoint of the GitHub a web URL lives on.
/// github.com answers on api.github.com; GitHub Enterprise Server on its own host under /api/v3/
/// and /api/graphql. Only the URL's scheme and authority are read, so a repository URL, a web root
/// and an API base on the same host all name the same endpoints.
/// </summary>
public sealed record GitHubApiEndpoints(Uri Rest, Uri GraphQl)
{
    public static readonly GitHubApiEndpoints GitHubCom =
        new(new Uri("https://api.github.com/"), new Uri("https://api.github.com/graphql"));

    /// <summary>The REST base without its trailing slash, for composing "{root}/repos/…".</summary>
    public string RestRoot => Rest.AbsoluteUri.TrimEnd('/');

    public static GitHubApiEndpoints For(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return GitHubCom;
        if (IsGitHubCom(uri.Host)) return GitHubCom;
        var root = $"{uri.Scheme}://{uri.Authority}";
        return new GitHubApiEndpoints(new Uri($"{root}/api/v3/"), new Uri($"{root}/api/graphql"));
    }

    private static bool IsGitHubCom(string host) =>
        host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
        || host.Equals("www.github.com", StringComparison.OrdinalIgnoreCase)
        || host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase);
}
