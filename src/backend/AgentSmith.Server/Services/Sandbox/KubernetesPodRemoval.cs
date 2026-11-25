using k8s;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992b: deletes a sandbox pod with no grace period and logs instead of throwing — the last step of every
/// teardown, single sandbox or workspace, so a failure here can never skip what ran before it.
/// </summary>
public sealed class KubernetesPodRemoval(IKubernetes client, ILogger logger)
{
    public async Task DeleteAsync(KubernetesContainerAddress address)
    {
        try
        {
            await client.CoreV1.DeleteNamespacedPodAsync(address.PodName, address.Namespace, gracePeriodSeconds: 0);
            logger.LogInformation("Sandbox pod {Pod} deleted", address.PodName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete sandbox pod {Pod}", address.PodName);
        }
    }
}
