using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Infrastructure.Services.Sandbox;

/// <summary>
/// p0419: translates the canonical sandbox paths into real host paths.
/// <para>
/// Steps speak one path language whatever the backend — <c>/work</c> for the working
/// tree, <c>/root</c> for the toolchain's home — because a container sandbox runs as
/// root in a container. In-process, both are directories on the operator's machine.
/// <c>/work</c> was already translated; <c>/root</c> was not, so staged feed
/// credentials hit "Read-only file system: /root" and the private feed stayed
/// unauthenticated. A canonical path that only half exists is worse than none.
/// </para>
/// </summary>
public sealed class SandboxPathMap
{
    private const string WorkRoot = "/work";
    private const string HomeRoot = "/root";

    private readonly string workDir;

    public SandboxPathMap(string jobId, string workDir)
    {
        this.workDir = workDir;
        var slug = jobId.Length > 12 ? jobId[..12] : jobId;
        HomeDir = Path.Combine(Path.GetTempPath(), $"agentsmith-home-{slug}");
    }

    /// <summary>Where this sandbox's <c>/root</c> lives on the host.</summary>
    public string HomeDir { get; }

    /// <summary>
    /// The package caches, SHARED across sandboxes and stable across runs — a cache that
    /// moved with the sandbox would be cold every time, which is the opposite of a cache.
    /// Which ecosystems exist is <see cref="PackageCacheCatalog"/>'s business, not this
    /// map's: here it is one more canonical root to rebase.
    /// </summary>
    public static string CacheDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".agentsmith", "pkgcache");

    public string Resolve(string raw)
    {
        if (TryUnder(raw, WorkRoot, workDir, out var underWork)) return underWork;
        if (TryUnder(raw, HomeRoot, HomeDir, out var underHome)) return underHome;
        if (TryUnder(raw, PackageCacheCatalog.Root, CacheDir, out var underCache)) return underCache;
        if (Path.IsPathRooted(raw)) return raw;
        return Path.GetFullPath(Path.Combine(workDir, raw));
    }

    private static bool TryUnder(string raw, string canonical, string real, out string resolved)
    {
        if (raw.Equals(canonical, StringComparison.Ordinal))
        {
            resolved = real;
            return true;
        }
        if (raw.StartsWith(canonical + "/", StringComparison.Ordinal))
        {
            resolved = Path.GetFullPath(Path.Combine(real, raw[(canonical.Length + 1)..]));
            return true;
        }
        resolved = string.Empty;
        return false;
    }
}
