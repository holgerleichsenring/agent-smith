using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0281a: expands a project's <c>connection/glob</c> repo references (with <c>!</c> excludes)
/// into concrete <see cref="RepoConnection"/> records, matching against the repos discovered
/// per connection. Reads the in-memory snapshot; on a cold connection it triggers a blocking
/// refresh (which loads live discovery or the durable last-good, or fails loud). The resolved
/// repo set is logged per connection so glob drift (a new repo silently matching) is visible.
/// </summary>
public sealed class RepoGlobExpander(
    IConnectionRepoSnapshot snapshot,
    IRepoDiscoveryRefresher refresher,
    ILogger<RepoGlobExpander> logger)
{
    public IReadOnlyList<RepoConnection> Expand(
        string project,
        IReadOnlyList<RepoGlobRef> refs,
        IReadOnlyDictionary<string, ResolvedConnection> connections)
    {
        var result = new List<RepoConnection>();
        foreach (var group in refs.GroupBy(r => r.Connection, StringComparer.OrdinalIgnoreCase))
            result.AddRange(ExpandConnection(project, group.Key, group.ToList(), connections));
        return result;
    }

    private IReadOnlyList<RepoConnection> ExpandConnection(
        string project, string connectionName, IReadOnlyList<RepoGlobRef> refs,
        IReadOnlyDictionary<string, ResolvedConnection> connections)
    {
        if (!connections.TryGetValue(connectionName, out var connection))
            throw new Domain.Exceptions.ConfigurationException(
                $"Project '{project}': repo reference uses connection '{connectionName}' which is not " +
                $"defined in connections: catalog.");

        var discovered = ResolveDiscovered(connection);
        var matched = SelectMatching(discovered, refs);
        LogResolved(project, connectionName, matched);
        return matched.Select(r => ToRepoConnection(connection, r)).ToList();
    }

    private IReadOnlyList<DiscoveredRepo> ResolveDiscovered(ResolvedConnection connection)
    {
        if (snapshot.TryGet(connection.Name, out var cached)) return cached;
        // Cold cache: block once to populate it (live discovery or durable last-good, or throw).
        refresher.RefreshAsync(connection, CancellationToken.None).GetAwaiter().GetResult();
        return snapshot.TryGet(connection.Name, out var fresh) ? fresh : Array.Empty<DiscoveredRepo>();
    }

    private static IReadOnlyList<DiscoveredRepo> SelectMatching(
        IReadOnlyList<DiscoveredRepo> discovered, IReadOnlyList<RepoGlobRef> refs)
    {
        var includes = refs.Where(r => !r.IsExclude).ToList();
        var excludes = refs.Where(r => r.IsExclude).ToList();
        return discovered
            .Where(repo => includes.Any(i => RepoGlobMatcher.IsMatch(i.Pattern, repo.Name)))
            .Where(repo => !excludes.Any(e => RepoGlobMatcher.IsMatch(e.Pattern, repo.Name)))
            .ToList();
    }

    private static RepoConnection ToRepoConnection(ResolvedConnection connection, DiscoveredRepo repo) =>
        new()
        {
            Name = repo.Name,
            Type = connection.Type,
            Url = repo.Url,
            Organization = connection.Organization,
            Project = connection.Project,
            Auth = connection.Auth,
            DefaultBranch = repo.DefaultBranch ?? connection.DefaultBranch,
        };

    private void LogResolved(string project, string connectionName, IReadOnlyList<DiscoveredRepo> matched) =>
        logger.LogInformation(
            "RepoGlobExpander: project '{Project}' connection '{Connection}' resolved {Count} repo(s): [{Names}]",
            project, connectionName, matched.Count, string.Join(", ", matched.Select(r => r.Name)));
}
