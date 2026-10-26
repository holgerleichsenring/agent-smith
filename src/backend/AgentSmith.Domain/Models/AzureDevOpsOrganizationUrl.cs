namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-10-26-cb25a: what an Azure DevOps organization URL is. The cloud has two spellings of one organization —
/// dev.azure.com/{org} and the legacy {org}.visualstudio.com — and a Server collection is whatever URL its
/// operator installed it under; this is the one place those hosts are known.
/// </summary>
public static class AzureDevOpsOrganizationUrl
{
    /// <summary>The cloud host, where the organization is the first path segment.</summary>
    public const string CloudHost = "dev.azure.com";

    /// <summary>The cloud's base URL: the organization URL is this plus "/{organization}".</summary>
    public const string CloudBase = "https://" + CloudHost;

    private const string LegacyHostSuffix = ".visualstudio.com";

    /// <summary>
    /// The cloud organization a dev.azure.com or {org}.visualstudio.com URL names — any URL of it, a repository,
    /// a work item or the organization itself — or null for a Server URL or text that is not a URL.
    /// </summary>
    public static string? CloudOrganizationOf(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        if (uri.Host.Equals(CloudHost, StringComparison.OrdinalIgnoreCase))
            return uri.AbsolutePath.Trim('/').Split('/')[0] is { Length: > 0 } organization ? organization : null;
        return uri.Host.EndsWith(LegacyHostSuffix, StringComparison.OrdinalIgnoreCase)
            ? uri.Host[..^LegacyHostSuffix.Length]
            : null;
    }
}
