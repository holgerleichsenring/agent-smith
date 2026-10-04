using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-26-5c85: a discovered GitLab repo can carry its subgroup path
/// ('team-a/api'), so the repo prefix of an agent-addressed path is no longer
/// "the first path segment". Resolve by the LONGEST registered key that either
/// IS the whole path or is followed by '/', so 'team-a/api' wins over a repo
/// named 'team-a' and a slash-named repo never falls into the unknown-prefix
/// error.
/// </summary>
internal static class RepoPathRouter
{
    internal static (string Key, SandboxStepRunner? Runner) MatchLongestKey(
        IReadOnlyDictionary<string, SandboxStepRunner> runners, string? path) =>
        Match(runners, path);

    /// <summary>2026-10-02-3f06c: the same rule over a turn's sandboxes, so a cited path is
    /// routed exactly as the tool that would read it routes it.</summary>
    internal static (string Key, ISandbox? Sandbox) MatchLongestKey(
        IReadOnlyDictionary<string, ISandbox> sandboxes, string? path) =>
        Match(sandboxes, path);

    private static (string Key, T? Value) Match<T>(IReadOnlyDictionary<string, T> entries, string? path)
        where T : class
    {
        if (string.IsNullOrEmpty(path)) return (string.Empty, null);
        var bestKey = string.Empty;
        T? best = null;
        foreach (var (key, value) in entries)
        {
            if (key.Length == 0 || key.Length > path.Length) continue;
            if (!path.StartsWith(key, StringComparison.Ordinal)) continue;
            if (path.Length > key.Length && path[key.Length] != '/') continue;
            if (best is null || key.Length > bestKey.Length) (bestKey, best) = (key, value);
        }
        return (bestKey, best);
    }
}
