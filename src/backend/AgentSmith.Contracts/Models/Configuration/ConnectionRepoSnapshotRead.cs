namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-10-02-b540: the repos a connection's hot snapshot holds and which store answered. A local
/// answer is yesterday's truth until a discovery says otherwise; its reader starts a refresh.
/// </summary>
public sealed record ConnectionRepoSnapshotRead(
    IReadOnlyList<DiscoveredRepo> Repos,
    ConnectionDiscoverySource Source = ConnectionDiscoverySource.Shared);
