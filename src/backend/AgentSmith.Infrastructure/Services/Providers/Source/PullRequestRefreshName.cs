namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-25-cc1f: the name a polled pull request's refresh is nudged under — its URL (the sweep names every repository's items in one set), target, head, draft flag, conflict state
/// and reviews, so a change to any of them is a new name and re-reads the record once, and an unchanged one is not read again.
/// </summary>
public static class PullRequestRefreshName
{
    public static string Of(string? url, string? target, string? head, string? draft, string? conflict, IEnumerable<string> reviews) =>
        $"refresh|{url}|{target}|{head}|{draft}|{conflict}|{string.Join(',', reviews.Order(StringComparer.Ordinal))}";
}
