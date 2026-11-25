using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992b: what a single Docker sandbox and a run's Docker workspace share — the network, the package caches,
/// the ownership of the volumes, the ONE agent loader, and starting a toolchain container with its own agent. The
/// factories decide only how many containers there are and which volumes each one owns.
/// </summary>
public sealed class DockerSandboxLauncher(
    IDockerClient docker, IConnectionMultiplexer redis, DockerContainerSpecBuilder specBuilder,
    DockerSandboxOptions options, DockerPackageCaches packageCaches, DockerImagePresence images,
    DockerSandboxVolumes sandboxVolumes, DockerOneShotRunner oneShot,
    IOptions<SandboxGlobalConfig> sandboxConfig, IWireProtocolWatcher protocol, ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<DockerSandboxLauncher>();

    public Task<string> ResolveNetworkAsync(CancellationToken ct) =>
        new DockerSandboxNetwork(docker, options, _logger).ResolveAsync(ct);

    /// <summary>
    /// The caches, each container's volumes given to the profile's uid, then the loader injecting the agent into the
    /// shared volume once. <paramref name="owners"/> is each container's slug and the volumes it mounts.
    /// </summary>
    public async Task<IReadOnlyList<PackageCacheVolume>> PrepareAsync(
        string slug, IReadOnlyList<(string Slug, DockerSandboxVolumeSet Volumes)> owners, SandboxSpec spec,
        string network, CancellationToken ct)
    {
        var profile = spec.SecurityContext ?? SandboxSecurityContext.Unconfined;
        // p0407: the package caches are host state that outlives this sandbox, so they
        // are ensured (not created per run) and never handed to the per-job cleanup.
        var caches = spec.NoPackageCaches ? [] : await packageCaches.EnsureAsync(profile, ct); // 4778
        await images.EnsurePresentAsync(spec.AgentImage, isCarrier: true, ct);
        foreach (var (ownerSlug, volumes) in owners)
            await sandboxVolumes.GiveOwnershipAsync(ownerSlug, volumes, caches, spec.AgentImage, profile, ct);
        await RunLoaderAsync(slug, owners[0].Volumes.Shared, spec.AgentImage, profile, network, ct);
        return caches;
    }

    /// <summary>Starts one toolchain container; the sandbox removes <paramref name="ownedVolumes"/> with it.</summary>
    public async Task<DockerSandbox> StartAsync(
        string containerName, DockerSandboxVolumeSet volumes, SandboxBusCredential bus, string jobId,
        SandboxSpec spec, string network, IReadOnlyList<PackageCacheVolume> caches, IReadOnlyList<string> ownedVolumes,
        CancellationToken ct)
    {
        await images.EnsurePresentAsync(spec.ToolchainImage, isCarrier: false, ct);
        var containerSpec = specBuilder.BuildToolchain(containerName, volumes, jobId, bus.Url, spec, caches);
        if (containerSpec.HostConfig is not null) containerSpec.HostConfig.NetworkMode = network;
        var created = await docker.Containers.CreateContainerAsync(containerSpec, ct);
        await docker.Containers.StartContainerAsync(created.ID, new ContainerStartParameters(), ct);
        _logger.LogInformation("Sandbox container {Id} started for job {JobId}", created.ID, jobId);
        var channel = new SandboxRedisChannel(redis, jobId, loggerFactory.CreateLogger<SandboxRedisChannel>(), protocol);
        return new DockerSandbox(docker, created.ID, ownedVolumes, jobId, channel, bus,
            // p0230: per-project resolved cap rides on the spec; fall back to global.
            spec.StepTimeoutSeconds ?? sandboxConfig.Value.StepTimeoutSeconds,
            spec.ToolchainImage, loggerFactory.CreateLogger<DockerSandbox>());
    }

    /// <summary>Removes a workspace's volumes once its containers are gone; a failure is logged, never thrown.</summary>
    public async Task RemoveVolumesAsync(IReadOnlyList<string> names)
    {
        foreach (var name in names)
        {
            try { await docker.Volumes.RemoveAsync(name, force: true); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to remove sandbox volume {Name}", name); }
        }
    }

    private async Task RunLoaderAsync(
        string slug, string sharedVolume, string agentImage, SandboxSecurityContext profile, string network, CancellationToken ct)
    {
        var spec = specBuilder.BuildLoader($"agentsmith-sandbox-loader-{slug}", sharedVolume, agentImage, profile);
        if (spec.HostConfig is not null) spec.HostConfig.NetworkMode = network;
        var exit = await oneShot.RunAsync(spec, ct);
        if (!exit.Succeeded)
            throw new InvalidOperationException(
                $"Sandbox-agent loader exited with code {exit.StatusCode} — `{agentImage}` failed " +
                $"to inject /shared/agent. Without the binary in place the toolchain crashes with " +
                $"the misleading 'exec /shared/agent: no such file or directory'. Loader output: " +
                exit.LogsOrEmpty);
    }
}
