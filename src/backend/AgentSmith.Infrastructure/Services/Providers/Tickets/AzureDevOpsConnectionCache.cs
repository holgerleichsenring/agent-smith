using System.Collections.Concurrent;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Providers.Source;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0147f: TTL-bounded <see cref="VssConnection"/> cache. ADO connections
/// are heavyweight (TLS handshake, federation token refresh); reusing them
/// across method calls is meaningful, but stale entries cause sporadic
/// 503/timeouts so the entries are rebuilt on TTL expiry or on transport
/// failure (the latter via <see cref="Invalidate"/>).
/// </summary>
internal sealed class AzureDevOpsConnectionCache(AzureDevOpsTicketConnection connection, ILogger logger)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);
    private static readonly ConcurrentDictionary<string, Entry> Cache = new();

    private readonly string _organizationUrl = connection.OrganizationUrl;
    private readonly string _pat = connection.PersonalAccessToken;

    // 2026-10-02-5f89a: keyed by org URL AND token hash, so a re-pointed secret or a second
    // tracker on one organization builds its own connection instead of borrowing the first.
    private readonly string _key = AzureDevOpsConnectionKey.For(
        connection.OrganizationUrl, connection.PersonalAccessToken);

    public WorkItemTrackingHttpClient CreateClient() =>
        Connection().GetClient<WorkItemTrackingHttpClient>();

    /// <summary>The pooled connection for this organization and token.</summary>
    internal VssConnection Connection() =>
        Cache.AddOrUpdate(_key,
            addValueFactory: _ => Build(_organizationUrl),
            updateValueFactory: (_, existing) =>
                existing.IsStale(Ttl) ? Rebuild(_organizationUrl, existing) : existing)
            .Connection;

    public void Invalidate(Exception cause)
    {
        if (Cache.TryRemove(_key, out _))
            logger.LogWarning(cause, "Evicting cached VssConnection for {Url}: {Message}",
                _organizationUrl, cause.Message);
    }

    private Entry Build(string url)
    {
        logger.LogInformation("Initializing VssConnection for {Url}", url);
        return new Entry(
            new VssConnection(new Uri(url), new VssBasicCredential(string.Empty, _pat)),
            DateTimeOffset.UtcNow);
    }

    private Entry Rebuild(string url, Entry old)
    {
        logger.LogInformation("Refreshing VssConnection for {Url} (TTL {Min}min reached)",
            url, Ttl.TotalMinutes);
        try { old.Connection.Dispose(); } catch { /* best-effort */ }
        return Build(url);
    }

    private sealed record Entry(VssConnection Connection, DateTimeOffset CreatedAt)
    {
        public bool IsStale(TimeSpan ttl) => DateTimeOffset.UtcNow - CreatedAt > ttl;
    }
}
