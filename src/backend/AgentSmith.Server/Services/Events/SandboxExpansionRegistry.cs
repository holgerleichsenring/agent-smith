using System.Collections.Concurrent;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// Tracks which (runId, repo) pairs have at least one subscriber that has
/// called <c>ExpandSandbox</c> on the hub. The broadcaster consults this
/// registry to decide whether SandboxOutput L3 events are fanned out (gate
/// on expansion) or dropped (no consumer).
/// </summary>
public sealed class SandboxExpansionRegistry
{
    private readonly ConcurrentDictionary<string, int> _counts = new(StringComparer.Ordinal);

    public void Expand(string runId, string repo) =>
        _counts.AddOrUpdate(Key(runId, repo), 1, (_, n) => n + 1);

    public void Collapse(string runId, string repo)
    {
        _counts.AddOrUpdate(Key(runId, repo), 0, (_, n) => Math.Max(0, n - 1));
    }

    public bool IsExpanded(string runId, string repo) =>
        _counts.TryGetValue(Key(runId, repo), out var n) && n > 0;

    private static string Key(string runId, string repo) => $"{runId}|{repo}";
}
