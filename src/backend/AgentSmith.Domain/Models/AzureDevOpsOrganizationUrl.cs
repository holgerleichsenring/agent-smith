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
    /// 2026-10-26-cb25b: the organization URL a configured base URL (a connection's host, a tracker's url) and an
    /// organization name make. No base, or a base on a cloud host, is the cloud: https://dev.azure.com/{organization},
    /// whatever path the base carries. A Server base gets the escaped organization (its collection) appended, unless
    /// its last path segment already is it — then the base is the collection URL itself.
    /// </summary>
    public static string For(string? baseUrl, string organization)
    {
        var escaped = Uri.EscapeDataString(organization);
        if (string.IsNullOrWhiteSpace(baseUrl)) return $"{CloudBase}/{escaped}";
        var trimmed = baseUrl.Trim().TrimEnd('/');
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return $"{trimmed}/{escaped}";
        if (IsCloudHost(uri.Host)) return $"{CloudBase}/{escaped}";
        var last = uri.AbsolutePath.TrimEnd('/').Split('/')[^1];
        return string.Equals(Uri.UnescapeDataString(last), organization, StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed}/{escaped}";
    }

    /// <summary>
    /// The cloud organization a dev.azure.com or {org}.visualstudio.com URL names — any URL of it, a repository,
    /// a work item or the organization itself — or null for a Server URL or text that is not a URL.
    /// </summary>
    public static string? CloudOrganizationOf(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        if (IsDevAzureHost(uri.Host))
            return uri.AbsolutePath.Trim('/').Split('/')[0] is { Length: > 0 } organization ? organization : null;
        return uri.Host.EndsWith(LegacyHostSuffix, StringComparison.OrdinalIgnoreCase)
            ? uri.Host[..^LegacyHostSuffix.Length]
            : null;
    }

    private static bool IsCloudHost(string host) =>
        IsDevAzureHost(host) || host.EndsWith(LegacyHostSuffix, StringComparison.OrdinalIgnoreCase);

    private static bool IsDevAzureHost(string host) => host.Equals(CloudHost, StringComparison.OrdinalIgnoreCase);
}
