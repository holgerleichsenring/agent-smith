using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-25-9dbba: how a GitLab merge request names its ticket. An issue of this project is
/// '#N', an issue of another project on the same instance is 'group/project#N' — both auto-link
/// and neither closes. Any other ticket gets its web url.
/// </summary>
public static class GitLabTicketReference
{
    private const string IssueMarker = "/-/issues/";

    /// <summary>
    /// The merge request description: the description plus the ticket's reference.
    /// <paramref name="projectPath"/> is the unescaped 'group/project' path.
    /// </summary>
    public static string Compose(
        string description, PullRequestTicket? ticket, string baseUrl, string projectPath)
    {
        if (ticket?.Tracker != TrackerType.GitLab || !TryParseIssue(ticket.WebUrl, baseUrl, out var issue))
            return TicketReferenceFooter.AppendWebLink(description, ticket);
        var reference = string.Equals(issue.Path, projectPath, StringComparison.OrdinalIgnoreCase)
            ? $"#{issue.Number}"
            : $"{issue.Path}#{issue.Number}";
        return TicketReferenceFooter.Append(description, reference);
    }

    private static bool TryParseIssue(string? webUrl, string baseUrl, out (string Path, int Number) issue)
    {
        issue = default;
        var instance = baseUrl.TrimEnd('/') + "/";
        if (webUrl is null || !webUrl.StartsWith(instance, StringComparison.OrdinalIgnoreCase)) return false;
        var rest = webUrl[instance.Length..].Split('?', '#')[0];
        var marker = rest.IndexOf(IssueMarker, StringComparison.Ordinal);
        if (marker <= 0 || !int.TryParse(rest[(marker + IssueMarker.Length)..].TrimEnd('/'), out var number))
            return false;
        issue = (rest[..marker], number);
        return true;
    }
}
