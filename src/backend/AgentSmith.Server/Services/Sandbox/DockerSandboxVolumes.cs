using AgentSmith.Contracts.Sandbox;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-10-25-c5a7b: creates a sandbox's per-job volumes and, for a hardened sandbox, gives
/// them to its uid. A fresh volume is root-owned, so before the toolchain starts one short
/// container from the agent carrier image chowns /work, /root and the top of each package
/// cache, holding CHOWN and nothing else, with no network. The carrier is Debian, so chown
/// exists in every agent version a project can pin; no agent flag is needed.
/// </summary>
public sealed class DockerSandboxVolumes(IDockerClient docker, DockerOneShotRunner runner)
{
    private const string WorkMount = DockerContainerSpecBuilder.WorkMount;

    public async Task<DockerSandboxVolumeSet> CreateAsync(
        string slug, SandboxSecurityContext profile, CancellationToken ct)
    {
        var volumes = DockerSandboxVolumeSet.For(slug, profile);
        foreach (var name in volumes.All)
            await docker.Volumes.CreateAsync(new VolumesCreateParameters { Name = name }, ct);
        return volumes;
    }

    public async Task GiveOwnershipAsync(
        string slug, DockerSandboxVolumeSet volumes, IReadOnlyList<PackageCacheVolume> caches,
        string agentImage, SandboxSecurityContext profile, CancellationToken ct)
    {
        if (profile.RunAsRoot || volumes.Home is null) return;
        var exit = await runner.RunAsync(BuildOwnership(slug, volumes, caches, agentImage, profile), ct);
        if (!exit.Succeeded)
            throw new InvalidOperationException(
                $"The ownership container exited with code {exit.StatusCode}: `{agentImage}` could not give "
                + $"the sandbox volumes to uid {profile.Uid}. Output: {exit.LogsOrEmpty}");
    }

    internal static CreateContainerParameters BuildOwnership(
        string slug, DockerSandboxVolumeSet volumes, IReadOnlyList<PackageCacheVolume> caches,
        string agentImage, SandboxSecurityContext profile) => new()
    {
        Name = $"agentsmith-sandbox-owner-{slug}",
        Image = agentImage,
        User = "0",
        Entrypoint = ["chown"],
        Cmd = [$"{profile.Uid}:{profile.Gid}", WorkMount, SandboxSecurityContext.Home,
            .. caches.Select(c => c.Cache.MountPath)],
        HostConfig = new HostConfig
        {
            AutoRemove = false,
            NetworkMode = "none",
            CapDrop = ["ALL"],
            CapAdd = ["CHOWN"],
            SecurityOpt = ["no-new-privileges"],
            Binds = [$"{volumes.Work}:{WorkMount}:nocopy", $"{volumes.Home}:{SandboxSecurityContext.Home}:nocopy",
                .. caches.Select(c => c.Bind)]
        }
    };
}
