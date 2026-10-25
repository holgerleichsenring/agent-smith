using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-25-9dbba: how a GitHub pull request names its ticket. An issue of this repository is
/// '#N', an issue of another repository on the same host is 'owner/repo#N' — both auto-link and
/// neither closes. Any other ticket gets its web url.
/// </summary>
public static class GitHubTicketReference
{
    /// <summary>The pull request body: the description plus the ticket's reference.</summary>
    public static string Compose(
        string description, PullRequestTicket? ticket, string repoUrl, string owner, string repo)
    {
        if (ticket?.Tracker != TrackerType.GitHub || !TryParseIssue(ticket.WebUrl, out var issue)
            || !string.Equals(issue.Host, new Uri(repoUrl).Host, StringComparison.OrdinalIgnoreCase))
            return TicketReferenceFooter.AppendWebLink(description, ticket);
        var sameRepo = string.Equals(issue.Owner, owner, StringComparison.OrdinalIgnoreCase)
            && string.Equals(issue.Repo, repo, StringComparison.OrdinalIgnoreCase);
        var reference = sameRepo ? $"#{issue.Number}" : $"{issue.Owner}/{issue.Repo}#{issue.Number}";
        return TicketReferenceFooter.Append(description, reference);
    }

    private static bool TryParseIssue(string? webUrl, out (string Host, string Owner, string Repo, int Number) issue)
    {
        issue = default;
        if (!Uri.TryCreate(webUrl, UriKind.Absolute, out var uri)) return false;
        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length != 4 || segments[2] != "issues" || !int.TryParse(segments[3], out var number))
            return false;
        issue = (uri.Host, segments[0], segments[1], number);
        return true;
    }
}
