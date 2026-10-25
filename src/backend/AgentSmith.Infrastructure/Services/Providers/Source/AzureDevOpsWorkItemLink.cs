using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-25-9dbba: whether an Azure Repos pull request links its ticket as a native work item.
/// Only an Azure DevOps ticket of the repository's own organization is one — a GitHub issue #12
/// is not work item 12. A ticket without a web url (an init run) is taken at its tracker's word.
/// </summary>
public static class AzureDevOpsWorkItemLink
{
    private const string CloudHost = "dev.azure.com";
    private const string LegacyHostSuffix = ".visualstudio.com";

    /// <summary>The work item id to link, or null when the ticket is not one of this organization.</summary>
    public static int? WorkItemIdFor(PullRequestTicket? ticket, string organizationUrl)
    {
        if (ticket?.Tracker != TrackerType.AzureDevOps || !int.TryParse(ticket.Id.Value, out var id))
            return null;
        if (string.IsNullOrWhiteSpace(ticket.WebUrl)) return id;
        return SameOrganization(ticket.WebUrl, organizationUrl) ? id : null;
    }

    private static bool SameOrganization(string webUrl, string organizationUrl)
    {
        var ticketOrg = CloudOrganization(webUrl);
        if (ticketOrg is not null)
            return string.Equals(ticketOrg, CloudOrganization(organizationUrl), StringComparison.OrdinalIgnoreCase);
        return webUrl.StartsWith(organizationUrl.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);
    }

    // dev.azure.com/{org} and {org}.visualstudio.com name the same organization.
    private static string? CloudOrganization(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        if (uri.Host.Equals(CloudHost, StringComparison.OrdinalIgnoreCase))
            return uri.AbsolutePath.Trim('/').Split('/')[0];
        return uri.Host.EndsWith(LegacyHostSuffix, StringComparison.OrdinalIgnoreCase)
            ? uri.Host[..^LegacyHostSuffix.Length]
            : null;
    }
}
