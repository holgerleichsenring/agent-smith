using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0201: force-removes a sandbox container and the two volumes named after its job
/// id. Extracted in p0465 so the orphan reaper decides and this removes — the reaper
/// keeps one reason to change.
/// </summary>
public sealed class DockerSandboxRemover(IDockerClient docker, ILogger<DockerSandboxRemover> logger)
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
        await RemoveLabelledVolumesAsync(jobId, cancellationToken);
    }

    private async Task RemoveLabelledVolumesAsync(string jobId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(jobId)) return;
        var slug = jobId.Length > 12 ? jobId[..12] : jobId;
        foreach (var name in new[] { $"agentsmith-sandbox-{slug}-shared", $"agentsmith-sandbox-{slug}-work" })
        {
            try { await docker.Volumes.RemoveAsync(name, force: true, cancellationToken); }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to remove sandbox volume {Name}", name);
            }
        }
    }
}
