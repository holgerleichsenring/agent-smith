using System.Collections.Concurrent;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// Production AzDo client factory: VssBasicCredential with empty username + PAT.
/// Registered as a DI singleton.
/// <para>
/// p0348: a <see cref="VssConnection"/> is heavyweight — its first
/// <c>GetClient&lt;&gt;</c> negotiates auth + service locations over TLS. This
/// factory used to <c>new</c> one on EVERY call, so a single ticket's footprint
/// calc (read each repo's <c>.agentsmith/contexts</c> tree — one connection per
/// file/dir read) paid 10-20 sequential handshakes and drove the synchronous
/// poll toward its 20s ceiling. Connections are now cached per organization URL
/// with a TTL, the same trick the ticket side already uses
/// (AzureDevOpsConnectionCache): the handshake is paid once per 30 min, not per
/// read. Clients built from a connection are lightweight, so only the connection
/// is pooled.
/// </para>
/// </summary>
public sealed class DefaultAzDoClientFactory : IAzDoClientFactory
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);
    private static readonly ConcurrentDictionary<string, Entry> Cache = new();

    public GitHttpClient CreateGitClient(string organizationUrl, string personalAccessToken)
    {
        return Connect(organizationUrl, personalAccessToken).GetClient<GitHttpClient>();
    }

    public Task<GitHttpClient> CreateGitClientAsync(
        string organizationUrl, string personalAccessToken, CancellationToken cancellationToken)
    {
        return Connect(organizationUrl, personalAccessToken).GetClientAsync<GitHttpClient>(cancellationToken);
    }

    public WorkItemTrackingHttpClient CreateWorkItemClient(string organizationUrl, string personalAccessToken)
    {
        return Connect(organizationUrl, personalAccessToken).GetClient<WorkItemTrackingHttpClient>();
    }

    // Keyed by org URL (the PAT is stable per org, same as the ticket-side cache).
    // A stale entry (TTL reached) is disposed and rebuilt so a refreshed
    // federation token / rotated location cache never wedges the connection.
    private static VssConnection Connect(string organizationUrl, string personalAccessToken)
    {
        var entry = Cache.AddOrUpdate(
            organizationUrl,
            addValueFactory: url => Build(url, personalAccessToken),
            updateValueFactory: (url, existing) =>
                existing.IsStale(Ttl) ? Rebuild(url, personalAccessToken, existing) : existing);
        return entry.Connection;
    }

    private static Entry Build(string url, string pat) =>
        new(new VssConnection(new Uri(url), new VssBasicCredential(string.Empty, pat)), DateTimeOffset.UtcNow);

    private static Entry Rebuild(string url, string pat, Entry old)
    {
        try { old.Connection.Dispose(); } catch { /* best-effort */ }
        return Build(url, pat);
    }

    private sealed record Entry(VssConnection Connection, DateTimeOffset CreatedAt)
    {
        public bool IsStale(TimeSpan ttl) => DateTimeOffset.UtcNow - CreatedAt > ttl;
    }
}
