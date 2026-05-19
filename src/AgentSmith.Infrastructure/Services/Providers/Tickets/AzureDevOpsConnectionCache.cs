using System.Collections.Concurrent;
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
internal sealed class AzureDevOpsConnectionCache(string organizationUrl, string pat, ILogger logger)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);
    private static readonly ConcurrentDictionary<string, Entry> Cache = new();

    public WorkItemTrackingHttpClient CreateClient() =>
        Cache.AddOrUpdate(organizationUrl,
            addValueFactory: Build,
            updateValueFactory: (url, existing) =>
                existing.IsStale(Ttl) ? Rebuild(url, existing) : existing)
            .Connection.GetClient<WorkItemTrackingHttpClient>();

    public void Invalidate(Exception cause)
    {
        if (Cache.TryRemove(organizationUrl, out _))
            logger.LogWarning(cause, "Evicting cached VssConnection for {Url}: {Message}",
                organizationUrl, cause.Message);
    }

    private Entry Build(string url)
    {
        logger.LogInformation("Initializing VssConnection for {Url}", url);
        return new Entry(
            new VssConnection(new Uri(url), new VssBasicCredential(string.Empty, pat)),
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
