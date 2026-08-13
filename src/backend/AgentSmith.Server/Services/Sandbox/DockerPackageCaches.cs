using AgentSmith.Contracts.Sandbox;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0407: backs every cache in <see cref="PackageCacheCatalog"/> with a Docker volume
/// that outlives the sandbox. The volumes are deliberately not per-job: they carry no
/// run identity, are never removed on sandbox dispose, and do not match the per-job
/// names the orphan reaper deletes — that persistence IS the cache.
/// </summary>
public sealed class DockerPackageCaches(
    IDockerClient docker,
    DockerSandboxOptions options,
    ILogger<DockerPackageCaches> logger)
{
    private const string VolumePrefix = "agentsmith-pkgcache";

    /// <summary>
    /// The cache volumes to mount. Empty when the operator turned the cache off
    /// (SANDBOX_PACKAGE_CACHE=false), which leaves the sandbox exactly as it was:
    /// no bind, no env, a cold restore every run.
    /// </summary>
    public IReadOnlyList<PackageCacheVolume> Volumes => options.PackageCacheEnabled
        ? [.. PackageCacheCatalog.All.Select(c => new PackageCacheVolume(VolumeName(c), c))]
        : [];

    /// <summary>
    /// Creates the cache volumes that do not exist yet and returns them for mounting.
    /// Docker's volume create is idempotent for an existing name, so a warm host simply
    /// gets its existing volumes back.
    /// </summary>
    public async Task<IReadOnlyList<PackageCacheVolume>> EnsureAsync(CancellationToken cancellationToken)
    {
        var volumes = Volumes;
        foreach (var volume in volumes)
        {
            await docker.Volumes.CreateAsync(
                new VolumesCreateParameters { Name = volume.VolumeName }, cancellationToken);
        }
        if (volumes.Count > 0)
        {
            logger.LogDebug("Package caches ready: {Ecosystems}",
                string.Join(", ", volumes.Select(v => v.Cache.Ecosystem)));
        }
        return volumes;
    }

    private static string VolumeName(PackageCacheMount cache) => $"{VolumePrefix}-{cache.Ecosystem}";
}
