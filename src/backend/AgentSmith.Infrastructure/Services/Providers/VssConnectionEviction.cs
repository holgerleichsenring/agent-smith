using System.Collections.Concurrent;

namespace AgentSmith.Infrastructure.Services.Providers;

/// <summary>
/// 2026-11-25-d0e2: the Azure DevOps connection caches key on url and token, and a service principal's token changes every hour,
/// so a key is never asked again once its token rotated. Before each lookup every OTHER entry past its time to live is removed;
/// it is not disposed, because a client taken from it may still be at work, and the collector frees it once nobody holds it.
/// The asked key keeps its own rebuild.
/// </summary>
internal static class VssConnectionEviction
{
    public static void DropStale<TEntry>(
        ConcurrentDictionary<string, TEntry> cache, string asked, Func<TEntry, bool> stale)
    {
        foreach (var (key, entry) in cache)
            if (key != asked && stale(entry)) cache.TryRemove(new KeyValuePair<string, TEntry>(key, entry));
    }
}
