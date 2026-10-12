using System.Text;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-f5f7e: the sentence a ticket comment sends a reader to the spec pull requests with —
/// one URL as before, several as a list by repository, and a repository the series no longer names
/// listed as dropped, because its pull request is still open and is left to people to close.
/// </summary>
public static class SpecPullRequestLines
{
    /// <param name="pullRequests">Repository → spec pull request URL.</param>
    /// <param name="seriesRepos">The repositories the series names; null lists every one as a member.</param>
    /// <param name="lead">The sentence before the URL(s), without a colon.</param>
    public static string Render(
        IReadOnlyDictionary<string, string>? pullRequests, IReadOnlyList<string>? seriesRepos, string lead)
    {
        if (pullRequests is null || pullRequests.Count == 0) return string.Empty;
        var members = pullRequests.Where(pr => IsMember(pr.Key, seriesRepos)).ToList();
        var dropped = pullRequests.Where(pr => !IsMember(pr.Key, seriesRepos)).ToList();
        if (members.Count == 1 && dropped.Count == 0) return $"{lead}: {members[0].Value}";
        var sb = new StringBuilder($"{lead}:");
        foreach (var (repo, url) in members) sb.Append($"\n- {repo}: {url}");
        foreach (var (repo, url) in dropped)
            sb.Append($"\n- {repo}: {url} — dropped from the series; this pull request is still open");
        return sb.ToString();
    }

    private static bool IsMember(string repo, IReadOnlyList<string>? seriesRepos) =>
        seriesRepos is null || seriesRepos.Contains(repo, StringComparer.OrdinalIgnoreCase);
}
