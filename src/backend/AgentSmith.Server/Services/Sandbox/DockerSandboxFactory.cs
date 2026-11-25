using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Docker.DotNet;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// Creates the agent-loader + toolchain container pair on Docker. The per-job volumes and their
/// ownership are <see cref="DockerSandboxVolumes"/>'s (2026-10-25-c5a7b). 2026-11-25-9992b: the launch itself is
/// <see cref="DockerSandboxLauncher"/>'s, shared with <see cref="DockerWorkspaceFactory"/>; this sandbox owns every
/// volume it created and removes them with its container.
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
    private readonly DockerSandboxLauncher _launcher = new(docker, redis, specBuilder, options, packageCaches, images,
        sandboxVolumes, oneShot, sandboxConfig, protocol, loggerFactory);

    public async Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var slug = jobId[..12];
        var profile = spec.SecurityContext ?? SandboxSecurityContext.Unconfined;
        var network = await _launcher.ResolveNetworkAsync(cancellationToken);
        // c5a7e; 4778: issued before anything is created, so a refused shared credential leaks no volume or loader.
        var bus = await busCredentials.IssueAsync(jobId, options.RedisUrl, spec.RequiresPerJobBus);

        var volumes = await sandboxVolumes.CreateAsync(slug, profile, cancellationToken);
        var caches = await _launcher.PrepareAsync(slug, [(slug, volumes)], spec, network, cancellationToken);
        return await _launcher.StartAsync($"agentsmith-sandbox-{slug}", volumes, bus, jobId, spec, network, caches,
            volumes.All, cancellationToken);
    }
}
