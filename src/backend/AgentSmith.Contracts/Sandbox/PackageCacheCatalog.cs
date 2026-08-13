namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// p0407: the package caches a sandbox is given, as DATA. One row per ecosystem —
/// its mount path and the env vars that point its tooling there. Adding an ecosystem
/// is a row here and nothing else: no sandbox-creation code knows a package manager's
/// name, and no backend branches on the repo's language.
/// <para>
/// Every row is mounted into every sandbox rather than selected by the resolved
/// language. Selection would be a wrong answer waiting to happen: a repo's language
/// resolves to a single value while real repos are polyglot (a .NET service with a
/// frontend restores from two ecosystems in one sandbox), and an un-init repo
/// resolves to a synthetic default with no language at all — exactly the repo that
/// would then silently pay a cold restore. An unused cache costs an empty directory.
/// </para>
/// <para>
/// The invariant that keeps a row cheap: an ecosystem qualifies when a plain env var
/// points it at a directory. Maven is deliberately absent — it needs an argument or a
/// settings.xml, so it is not a row, and pretending otherwise would put ecosystem glue
/// back into the sandbox.
/// </para>
/// </summary>
public static class PackageCacheCatalog
{
    /// <summary>Root under which every cache is mounted inside the sandbox.</summary>
    public const string Root = "/pkgcache";

    private static readonly PackageCacheMount[] Entries =
    [
        // .NET: the global-packages folder holds every extracted package (the
        // expensive part); the http cache holds the downloaded .nupkg responses so a
        // partially-warm restore does not re-download what it already saw.
        Mount("nuget", new()
        {
            ["NUGET_PACKAGES"] = $"{Root}/nuget/packages",
            ["NUGET_HTTP_CACHE_PATH"] = $"{Root}/nuget/http-cache"
        }),
        Mount("npm", new() { ["NPM_CONFIG_CACHE"] = $"{Root}/npm" }),
        Mount("pip", new() { ["PIP_CACHE_DIR"] = $"{Root}/pip" }),
        // Go splits module downloads from compiled build output; both are re-fetched
        // or re-built from scratch in a fresh container, so both live in the cache.
        Mount("go", new()
        {
            ["GOMODCACHE"] = $"{Root}/go/mod",
            ["GOCACHE"] = $"{Root}/go/build"
        }),
        Mount("cargo", new() { ["CARGO_HOME"] = $"{Root}/cargo" })
    ];

    /// <summary>Every cache a sandbox is given, in catalog order.</summary>
    public static IReadOnlyList<PackageCacheMount> All => Entries;

    private static PackageCacheMount Mount(string ecosystem, Dictionary<string, string> env) =>
        new(ecosystem, $"{Root}/{ecosystem}", env);
}
