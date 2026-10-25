using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// Creates the agent-loader + toolchain container pair on Docker. The per-job volumes and their
/// ownership are <see cref="DockerSandboxVolumes"/>'s (2026-10-25-c5a7b).
/// </summary>
public sealed class DockerSandboxFactory(
    IDockerClient docker,
    IConnectionMultiplexer redis,
    DockerContainerSpecBuilder specBuilder,
    DockerSandboxOptions options,
    DockerPackageCaches packageCaches, DockerImagePresence images,
    DockerSandboxVolumes sandboxVolumes, DockerOneShotRunner oneShot,
    IOptions<SandboxGlobalConfig> sandboxConfig, IWireProtocolWatcher protocol,
    SandboxBusCredentials busCredentials, ILoggerFactory loggerFactory) : ISandboxFactory
{
    public async Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var slug = jobId[..12];
        var profile = spec.SecurityContext ?? SandboxSecurityContext.Unconfined;
        var network = await ResolveNetworkAsync(cancellationToken);

        var volumes = await sandboxVolumes.CreateAsync(slug, profile, cancellationToken);
        // p0407: the package caches are host state that outlives this sandbox, so they
        // are ensured (not created per run) and never handed to the per-job cleanup.
        var caches = await packageCaches.EnsureAsync(profile, cancellationToken);
        await images.EnsurePresentAsync(spec.AgentImage, isCarrier: true, cancellationToken);
        await sandboxVolumes.GiveOwnershipAsync(slug, volumes, caches, spec.AgentImage, profile, cancellationToken);
        await RunLoaderAsync(slug, volumes.Shared, spec.AgentImage, profile, network, cancellationToken);
        var bus = await busCredentials.IssueAsync(jobId, options.RedisUrl); // 2026-10-25-c5a7e
        var toolchainId = await StartToolchainAsync(
            slug, volumes, jobId, spec, network, caches, bus.Url, cancellationToken);

        var channel = new SandboxRedisChannel(
            redis, jobId, loggerFactory.CreateLogger<SandboxRedisChannel>(), protocol);
        return new DockerSandbox(docker, toolchainId, volumes.All, jobId, channel, bus,
            // p0230: per-project resolved cap rides on the spec; fall back to global.
            spec.StepTimeoutSeconds ?? sandboxConfig.Value.StepTimeoutSeconds,
            spec.ToolchainImage, loggerFactory.CreateLogger<DockerSandbox>());
    }

    // Explicit override first, then auto-detect from the server's own container, fallback to bridge.
    // Without this the sandbox lands on Docker's default bridge with no DNS for
    // `redis` / `host.docker.internal`, and the sandbox-agent's Redis connect
    // times out with a confusing UnableToConnect.
    private async Task<string> ResolveNetworkAsync(CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger<DockerSandboxFactory>();
        if (!string.IsNullOrWhiteSpace(options.Network))
        {
            logger.LogDebug("Using configured sandbox network: {Network}", options.Network);
            return options.Network;
        }
        try
        {
            var hostname = System.Net.Dns.GetHostName();
            var info = await docker.Containers.InspectContainerAsync(hostname, ct);
            var network = info.NetworkSettings?.Networks?.Keys.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(network))
            {
                logger.LogDebug("Auto-detected sandbox network: {Network}", network);
                return network;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug("Could not auto-detect sandbox network: {Message}", ex.Message);
        }
        logger.LogDebug("Falling back to bridge network for sandbox");
        return "bridge";
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

    private async Task<string> StartToolchainAsync(
        string slug, DockerSandboxVolumeSet volumes, string jobId,
        SandboxSpec spec, string network, IReadOnlyList<PackageCacheVolume> caches, string redisUrl, CancellationToken ct)
    {
        await images.EnsurePresentAsync(spec.ToolchainImage, isCarrier: false, ct);
        var containerSpec = specBuilder.BuildToolchain(
            $"agentsmith-sandbox-{slug}", volumes, jobId, redisUrl, spec, caches);
        if (containerSpec.HostConfig is not null) containerSpec.HostConfig.NetworkMode = network;
        var created = await docker.Containers.CreateContainerAsync(containerSpec, ct);
        await docker.Containers.StartContainerAsync(created.ID, new ContainerStartParameters(), ct);
        loggerFactory.CreateLogger<DockerSandboxFactory>()
            .LogInformation("Sandbox container {Id} started for job {JobId}", created.ID, jobId);
        return created.ID;
    }
}
