using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992b: the volumes of a run's Docker workspace — ONE shared (the agent binary) and ONE work volume named
/// after the workspace slug, and for a hardened profile a home volume PER CONTAINER, named after that container's own
/// job slug, so what one toolchain stages under ~ stays its own. The first container's slug is the workspace slug, so
/// its volumes carry the names a single sandbox's would, and the orphan remover's by-job-id names reach every one.
/// </summary>
public sealed record DockerWorkspaceVolumes(string Shared, string Work, IReadOnlyList<string?> Homes)
{
    public static DockerWorkspaceVolumes For(IReadOnlyList<string> containerSlugs, SandboxSecurityContext profile)
    {
        var root = DockerSandboxVolumeSet.For(containerSlugs[0], profile);
        return new(root.Shared, root.Work, [.. containerSlugs.Select(s => DockerSandboxVolumeSet.For(s, profile).Home)]);
    }

    /// <summary>What the container at <paramref name="index"/> mounts: the shared pair and its own home.</summary>
    public DockerSandboxVolumeSet Container(int index) => new(Shared, Work, Homes[index]);

    public IReadOnlyList<string> All => [Shared, Work, .. Homes.OfType<string>()];
}
