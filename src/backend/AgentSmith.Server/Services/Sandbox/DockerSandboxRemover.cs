using AgentSmith.Contracts.Sandbox;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0201: force-removes a sandbox container and the volumes named after its job
/// id. Extracted in p0465 so the orphan reaper decides and this removes — the reaper
/// keeps one reason to change.
/// </summary>
public sealed class DockerSandboxRemover(
    IDockerClient docker, SandboxBusCredentials busCredentials, ILogger<DockerSandboxRemover> logger)
{
    public async Task RemoveAsync(string containerId, string jobId, CancellationToken cancellationToken)
    {
        try
        {
            await docker.Containers.RemoveContainerAsync(containerId,
                new ContainerRemoveParameters { Force = true, RemoveVolumes = false }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to remove orphan sandbox container {Id}", containerId);
            return;
        }
        await busCredentials.DeleteAsync(jobId); // 2026-10-25-c5a7e
        await RemoveLabelledVolumesAsync(jobId, cancellationToken);
    }

    private async Task RemoveLabelledVolumesAsync(string jobId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(jobId)) return;
        var slug = jobId.Length > 12 ? jobId[..12] : jobId;
        // 2026-10-25-c5a7b: every name a sandbox can own, home included; an absent one is a no-op.
        foreach (var name in DockerSandboxVolumeSet.For(slug, SandboxSecurityContext.Hardened).All)
        {
            try { await docker.Volumes.RemoveAsync(name, force: true, cancellationToken); }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to remove sandbox volume {Name}", name);
            }
        }
    }
}
