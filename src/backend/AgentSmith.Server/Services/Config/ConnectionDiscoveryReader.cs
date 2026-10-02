using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Config;

/// <summary>
/// 2026-10-02-5f89c: what the studio shows of a connection's discovery, read from the store every
/// replica shares. A connection nothing was recorded for is discovered AT ONCE — a flushed key
/// waits for no sweep — and the read waits up to <see cref="FirstDiscoveryWait"/> before
/// answering "discovering". Refresh now runs the same single-flight refresh and answers the view.
/// 2026-10-02-b540: a LOCAL answer — this server's last-good list, the shared store having none —
/// is served at once, marked local, and the refresh starts behind it.
/// </summary>
public sealed class ConnectionDiscoveryReader(
    IConfigStore configStore,
    IConfigurationLoader loader,
    IConnectionRepoSnapshotStore store,
    IRepoDiscoveryRefresher refresher)
{
    public static readonly TimeSpan FirstDiscoveryWait = TimeSpan.FromSeconds(30);

    /// <summary>The view, or null when no connection has this id.</summary>
    public async Task<ConnectionReposView?> ReadAsync(string id, CancellationToken cancellationToken)
    {
        if (configStore.GetConnections().All(c => !ConfigNames.AreSame(c.Id, id))) return null;
        var status = await store.TryGetDiscoveryAsync(id, cancellationToken);
        if (status is null) return await RefreshAsync(id, FirstDiscoveryWait, cancellationToken);
        if (status.Source == ConnectionDiscoverySource.Local) _ = RefreshAsync(id, Timeout.InfiniteTimeSpan, CancellationToken.None);
        return View(status);
    }

    /// <summary>Refreshes now and answers the view, or null when no connection has this id.</summary>
    public Task<ConnectionReposView?> RefreshNowAsync(string id, CancellationToken cancellationToken) =>
        configStore.GetConnections().Any(c => ConfigNames.AreSame(c.Id, id))
            ? RefreshAsync(id, Timeout.InfiniteTimeSpan, cancellationToken)
            : Task.FromResult<ConnectionReposView?>(null);

    private async Task<ConnectionReposView?> RefreshAsync(string id, TimeSpan wait, CancellationToken cancellationToken)
    {
        // The loader is the only path from a stored entry to the connection a run would use —
        // the legacy credential migration included.
        // A stored entry the loaded configuration dropped (a blocking finding) has nothing to run.
        var connection = loader.LoadConfig(string.Empty).Connections
            .FirstOrDefault(c => ConfigNames.AreSame(c.Key, id)).Value;
        if (connection is null) return new ConnectionReposView(null, []);
        try
        {
            await refresher.RefreshAsync(connection, cancellationToken).WaitAsync(wait, cancellationToken);
        }
        catch (TimeoutException)
        {
            return new ConnectionReposView(null, [], Discovering: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Recorded by the refresher; the status below carries it.
        }
        var status = await store.TryGetDiscoveryAsync(id, cancellationToken);
        return status is null ? new ConnectionReposView(null, []) : View(status);
    }

    private static ConnectionReposView View(ConnectionDiscoveryStatus status) => new(
        status.DiscoveredAt,
        [.. status.Repos.Select(r => new ConnectionRepoView(r.Name, r.DefaultBranch))],
        status.LastAttemptAt, status.LastError, status.RepoCount,
        Source: status.Source == ConnectionDiscoverySource.Local ? "local" : "shared");
}
