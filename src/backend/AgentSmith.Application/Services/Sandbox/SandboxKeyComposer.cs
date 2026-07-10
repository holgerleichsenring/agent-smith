namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Composes the operator-facing sandbox key. p0180: keying is per
/// (repo, toolchain group), not per context. Multiple same-toolchain
/// contexts on one repo share one sandbox and one key.
///
///   single-repo, single-group  → "default"
///   single-repo, multi-group   → "&lt;contextName&gt;"
///   multi-repo,  single-group  → "&lt;repo&gt;"
///   multi-repo,  multi-group   → "&lt;repo&gt;-&lt;contextName&gt;"
/// </summary>
public static class SandboxKeyComposer
{
    private const string DefaultContextName = "default";

    /// <summary>
    /// p0161a entry point: per-discovery composition. Retained for callers
    /// that still need per-context keys; the per-toolchain pipeline (p0180)
    /// uses <see cref="ComposeForGroup"/> instead.
    /// </summary>
    public static string Compose(
        int repoCount, string repoName, int perRepoDiscoveryCount, string contextName)
    {
        var isMultiRepo = repoCount > 1;
        var isMultiContext = perRepoDiscoveryCount > 1;

        if (!isMultiRepo && !isMultiContext)
            return contextName;
        if (!isMultiRepo)
            return contextName;
        if (!isMultiContext)
            return repoName;
        return $"{repoName}/{contextName}";
    }

    /// <summary>
    /// p0180: per-toolchain-group composition. <paramref name="repoGroupCount"/>
    /// is the number of distinct toolchain groups in this repo's discoveries.
    /// When 1, the key stays plain (operator-facing common case:
    /// "sample-server" for five csharp contexts).
    ///
    /// p0322b: multi-group keys carry the group's representative CONTEXT NAME
    /// (the directory under .agentsmith/contexts/, unique per repo by
    /// construction) instead of lang+resource slugs. Groups differ by
    /// (image, resources); the old slugs showed the parts that are often
    /// identical across groups (lang, size) and hid the differing image, so
    /// distinct groups collided into the coordinator's numeric "-2" backstop.
    /// A speaking key reads "worker-api" (multi-repo) / "api" (single-repo).
    /// </summary>
    public static string ComposeForGroup(
        int repoCount, string repoName, int repoGroupCount, string contextName)
    {
        var isMultiRepo = repoCount > 1;
        var isMultiGroup = repoGroupCount > 1;

        if (!isMultiRepo && !isMultiGroup)
            return DefaultContextName;
        if (!isMultiRepo)
            return Sanitize(contextName);
        if (!isMultiGroup)
            return repoName;
        return $"{repoName}-{Sanitize(contextName)}";
    }

    // p0322b: context names are directory names, but keys travel into pod names
    // and dashboards — normalize like the coordinator's other slugs.
    private static string Sanitize(string raw) =>
        new(raw.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
}
