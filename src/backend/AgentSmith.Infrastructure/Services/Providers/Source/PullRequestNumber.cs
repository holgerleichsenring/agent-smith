using System.Text.RegularExpressions;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-08-0781: the number a pull-request URL ends in, in the three shapes the source providers
/// build — GitHub /pull/N, GitLab /merge_requests/N, Azure Repos /pullrequest/N — or null.
/// </summary>
public static partial class PullRequestNumber
{
    public static string? FromUrl(string? url) =>
        url is not null && Shape().Match(url) is { Success: true } m ? m.Groups["n"].Value : null;

    [GeneratedRegex(@"/(?:pull|merge_requests|pullrequest)/(?<n>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex Shape();
}
