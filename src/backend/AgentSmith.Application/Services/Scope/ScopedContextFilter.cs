using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// p0336b: keep only the contexts scoping left for a repository. No entry for the repository (or
/// no scoping at all) keeps ALL its contexts; a filter that would empty it is ignored, so a kept
/// repository never provisions zero sandboxes. 2026-11-16-f3fba: shared by the coordinator and the
/// preflight's inventory, so the contexts judged before checkout are the ones the sandboxes get.
/// </summary>
public static class ScopedContextFilter
{
    public static IReadOnlyList<RemoteContextDiscovery> Apply(
        IReadOnlyList<RemoteContextDiscovery> discoveries, string? repoName,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? scopedContexts)
    {
        if (scopedContexts is null
            || !scopedContexts.TryGetValue(repoName ?? string.Empty, out var kept))
            return discoveries;
        var filtered = discoveries
            .Where(d => kept.Contains(d.ContextName, StringComparer.OrdinalIgnoreCase)).ToList();
        return filtered.Count == 0 ? discoveries : filtered;
    }
}
