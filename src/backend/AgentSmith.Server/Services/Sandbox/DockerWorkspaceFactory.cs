using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Docker.DotNet;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992b: a run's workspace on Docker — ONE volume set (<see cref="DockerWorkspaceVolumes"/>), ONE loader,
/// then a toolchain container per image (<c>agentsmith-sandbox-&lt;slug&gt;-&lt;n&gt;</c>), each its own job id, bus
/// credential and labels. A container owns no volume; the workspace removes them once, after every container.
/// <para>
/// The orphan remover works per container and removes the volumes named after that container's job id: its own home,
/// and for the first container the shared pair. A volume still mounted by a live sibling refuses removal (logged); the
/// daemon lists the newest container first, so the first container — whose job names the shared pair — is reached last.
/// </para>
/// </summary>
public sealed class DockerWorkspaceFactory(
    IDockerClient docker,
    IConnectionMultiplexer redis,
    DockerContainerSpecBuilder specBuilder,
    DockerSandboxOptions options,
    DockerPackageCaches packageCaches, DockerImagePresence images,
    DockerSandboxVolumes sandboxVolumes, DockerOneShotRunner oneShot,
    IOptions<SandboxGlobalConfig> sandboxConfig, IWireProtocolWatcher protocol,
    SandboxBusCredentials busCredentials, ILoggerFactory loggerFactory) : IWorkspaceFactory
{
    private readonly DockerSandboxLauncher _launcher = new(docker, redis, specBuilder, options, packageCaches, images,
        sandboxVolumes, oneShot, sandboxConfig, protocol, loggerFactory);

    public async Task<IWorkspace> CreateAsync(WorkspaceSpec spec, CancellationToken cancellationToken)
    {
        if (spec.Toolchains.Count == 0) throw new ArgumentException("A workspace needs a toolchain.", nameof(spec));
        var jobIds = spec.Toolchains.Select(_ => Guid.NewGuid().ToString("N")).ToList();
        var slugs = jobIds.Select(id => id[..12]).ToList();
        var slug = slugs[0];
        var profile = spec.Base.SecurityContext ?? SandboxSecurityContext.Unconfined;
        var network = await _launcher.ResolveNetworkAsync(cancellationToken);
        var buses = new List<SandboxBusCredential>(); // c5a7e; 4778: every credential before any volume
        foreach (var jobId in jobIds)
            buses.Add(await busCredentials.IssueAsync(jobId, options.RedisUrl, spec.Base.RequiresPerJobBus));

        var volumes = DockerWorkspaceVolumes.For(slugs, profile);
        await sandboxVolumes.CreateAsync(volumes.All, cancellationToken);
        var owners = slugs.Select((s, i) => (s, volumes.Container(i))).ToList();
        var caches = await _launcher.PrepareAsync(slug, owners, spec.Base, network, cancellationToken);
        var containers = new List<ISandbox>();
        for (var i = 0; i < spec.Toolchains.Count; i++)
        {
            var toolchain = spec.Toolchains[i];
            var containerSpec = spec.Base with { ToolchainImage = toolchain.Image, Resources = toolchain.Resources };
            containers.Add(await _launcher.StartAsync($"agentsmith-sandbox-{slug}-{i}", volumes.Container(i), buses[i],
                jobIds[i], containerSpec, network, caches, ownedVolumes: [], cancellationToken));
        }
        return new ContainerWorkspace(spec.Toolchains, containers, () => _launcher.RemoveVolumesAsync(volumes.All),
            loggerFactory.CreateLogger<ContainerWorkspace>());
    }
}
