using System.Collections.Concurrent;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Identity.Client;
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
/// and token (2026-10-02-5f89a) with a TTL, the same trick the ticket side already uses
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

    public IdentityHttpClient CreateIdentityClient(string organizationUrl, string personalAccessToken) =>
        Connect(organizationUrl, personalAccessToken).GetClient<IdentityHttpClient>();

    // 2026-10-08-f147: ConnectAsync reads connectionData; AuthorizedIdentity is who the PAT is.
    public async Task<string?> AuthorizedIdentityIdAsync(
        string organizationUrl, string personalAccessToken, CancellationToken cancellationToken)
    {
        var connection = Connect(organizationUrl, personalAccessToken);
        if (!connection.HasAuthenticated) await connection.ConnectAsync(cancellationToken);
        return connection.AuthorizedIdentity?.Id.ToString();
    }

    // 2026-10-02-5f89a: keyed by org URL AND token hash — two connections to one org, or a
    // re-pointed secret, each get their own connection. A stale entry (TTL reached) is disposed
    // and rebuilt so a refreshed federation token / rotated location cache never wedges it.
    internal static VssConnection Connect(string organizationUrl, string personalAccessToken)
    {
        var entry = Cache.AddOrUpdate(
            AzureDevOpsConnectionKey.For(organizationUrl, personalAccessToken),
            addValueFactory: _ => Build(organizationUrl, personalAccessToken),
            updateValueFactory: (_, existing) =>
                existing.IsStale(Ttl) ? Rebuild(organizationUrl, personalAccessToken, existing) : existing);
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
