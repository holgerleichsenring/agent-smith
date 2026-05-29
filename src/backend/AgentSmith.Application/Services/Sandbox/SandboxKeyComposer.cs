namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Composes the operator-facing sandbox key. p0180: keying is per
/// (repo, toolchain group), not per context. Multiple same-toolchain
/// contexts on one repo share one sandbox and one key.
///
///   single-repo, single-group  → "default" (or "&lt;langSlug&gt;" if non-default)
///   single-repo, multi-group   → "&lt;langSlug&gt;"
///   multi-repo,  single-group  → "&lt;repo&gt;"
///   multi-repo,  multi-group   → "&lt;repo&gt;-&lt;langSlug&gt;"
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
    /// When 1, the key drops the lang suffix (operator-facing common case:
    /// "sample-server" for five csharp contexts).
    /// When &gt;1, the key carries a lang slug so the per-group sandboxes are
    /// distinguishable ("sample-server-csharp" + "sample-server-typescript").
    /// </summary>
    public static string ComposeForGroup(
        int repoCount, string repoName, int repoGroupCount, string langSlug)
    {
        var isMultiRepo = repoCount > 1;
        var isMultiGroup = repoGroupCount > 1;

        if (!isMultiRepo && !isMultiGroup)
            return DefaultContextName;
        if (!isMultiRepo)
            return langSlug;
        if (!isMultiGroup)
            return repoName;
        return $"{repoName}-{langSlug}";
    }
}
