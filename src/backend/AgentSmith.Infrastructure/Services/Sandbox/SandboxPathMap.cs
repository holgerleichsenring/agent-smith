using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;

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
/// <para>
/// 2026-11-25-9992c: each <see cref="InProcessRepoMount"/> (2026-11-25-9992b: one per repository) serves a host directory at
/// <c>/work/&lt;repo&gt;</c>, ahead of <c>/work</c> itself, both ways.
/// </para>
/// </summary>
public sealed class SandboxPathMap
{
    private const string WorkRoot = Repository.WorkspaceRoot;
    private const string HomeRoot = "/root";

    private readonly string workDir;
    private readonly IReadOnlyList<InProcessRepoMount> mounts;

    public SandboxPathMap(string jobId, string workDir, IReadOnlyList<InProcessRepoMount>? mounts = null)
    {
        this.workDir = workDir;
        this.mounts = mounts ?? [];
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
        foreach (var mount in mounts)
            if (TryUnder(raw, Repository.WorkPathFor(mount.RepoName), mount.SourceDir, out var underRepo))
                return underRepo;
        if (TryUnder(raw, WorkRoot, workDir, out var underWork)) return underWork;
        if (TryUnder(raw, HomeRoot, HomeDir, out var underHome)) return underHome;
        if (TryUnder(raw, PackageCacheCatalog.Root, CacheDir, out var underCache)) return underCache;
        if (Path.IsPathRooted(raw)) return raw;
        return Path.GetFullPath(Path.Combine(workDir, raw));
    }

    /// <summary>The canonical path of a host path this map serves — the mounted repository first.</summary>
    public string Virtualise(string actual)
    {
        foreach (var mount in mounts)
            if (Rebase(actual, mount.SourceDir, Repository.WorkPathFor(mount.RepoName)) is { } underRepo)
                return underRepo;
        return Rebase(actual, workDir, WorkRoot) ?? actual;
    }

    private static string? Rebase(string actual, string real, string canonical)
    {
        var trimmed = real.TrimEnd(Path.DirectorySeparatorChar);
        if (actual.Equals(trimmed, StringComparison.Ordinal)) return canonical;
        return actual.StartsWith(trimmed + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? canonical + "/" + actual[(trimmed.Length + 1)..].Replace(Path.DirectorySeparatorChar, '/')
            : null;
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
