using System.Collections.Concurrent;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-10-13-9a59k: whether a work item's state is one its work item type files under the Completed
/// or Removed category — the tracker's own "closed", which no configured state name can say. A process
/// template defines its states per type, so the list is read per (organization, project, type) and
/// kept for the connection cache's 30 minutes: a hydration of 200 items costs one read per type. A read
/// that fails throws, like the work-item read beside it — an unknown category is never "open".
/// </summary>
internal sealed class AzureDevOpsStateCategories(
    string organizationUrl, string project,
    Func<string, CancellationToken, Task<List<WorkItemStateColor>>> readStates)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);
    private static readonly string[] ClosedCategories = ["Completed", "Removed"];
    private static readonly ConcurrentDictionary<string, Entry> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The categories of the project's types, read through the pooled connection.</summary>
    public static AzureDevOpsStateCategories For(AzureDevOpsConnectionCache connections, string project) =>
        new(connections.OrganizationUrl, project, (type, ct) =>
            connections.CreateClient().GetWorkItemTypeStatesAsync(project, type, cancellationToken: ct));

    /// <summary>Whether the fields' System.State is a closed state of their System.WorkItemType.</summary>
    public async Task<bool> IsClosedAsync(IDictionary<string, object> fields, CancellationToken cancellationToken)
    {
        if (Field(fields, "System.WorkItemType") is not { } type || Field(fields, "System.State") is not { } state)
            return false;
        return (await ClosedStatesAsync(type, cancellationToken)).Contains(state);
    }

    private async Task<IReadOnlySet<string>> ClosedStatesAsync(string type, CancellationToken cancellationToken)
    {
        var key = $"{organizationUrl.TrimEnd('/')}|{project}|{type}";
        if (Cache.TryGetValue(key, out var held) && DateTimeOffset.UtcNow - held.ReadAt <= Ttl) return held.Closed;
        var states = await readStates(type, cancellationToken);
        var closed = states
            .Where(s => s.Name is not null && ClosedCategories.Contains(s.Category, StringComparer.OrdinalIgnoreCase))
            .Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Cache[key] = new Entry(closed, DateTimeOffset.UtcNow);
        return closed;
    }

    private static string? Field(IDictionary<string, object> fields, string name) =>
        fields.TryGetValue(name, out var value) && value?.ToString() is { Length: > 0 } text ? text : null;

    private sealed record Entry(IReadOnlySet<string> Closed, DateTimeOffset ReadAt);
}
