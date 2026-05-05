using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// Polls Pod status until the toolchain container is Ready, throwing on
/// ImagePullBackOff / Failed phase, or on timeout (after best-effort delete).
/// </summary>
public sealed class KubernetesPodWatcher(IKubernetes client, ILogger logger)
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    public async Task WaitForReadyAsync(
        string podName, string ns, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        string? lastPhase = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pod = await client.CoreV1.ReadNamespacedPodAsync(podName, ns, cancellationToken: cancellationToken);
            var phase = pod.Status?.Phase;
            if (phase != lastPhase)
            {
                logger.LogInformation("Sandbox pod {Pod} phase: {Phase}", podName, phase);
                lastPhase = phase;
            }
            ThrowIfFailed(podName, pod);
            if (IsToolchainReady(pod)) return;
            await Task.Delay(PollInterval, cancellationToken);
        }

        await TryDeleteAsync(podName, ns);
        throw new TimeoutException(
            $"Sandbox pod {podName} did not become ready within {timeout.TotalSeconds:F0}s");
    }

    private static void ThrowIfFailed(string podName, V1Pod pod)
    {
        if (pod.Status?.Phase == "Failed")
            throw new InvalidOperationException(
                $"Sandbox pod {podName} entered Failed phase: {pod.Status.Reason ?? pod.Status.Message}");

        var statuses = pod.Status?.ContainerStatuses ?? [];
        foreach (var status in statuses)
        {
            var waiting = status.State?.Waiting;
            if (waiting?.Reason is "ImagePullBackOff" or "ErrImagePull" or "InvalidImageName")
                throw new InvalidOperationException(
                    $"Sandbox pod {podName} container {status.Name} failed to pull image: {waiting.Message}");
        }
    }

    private static bool IsToolchainReady(V1Pod pod)
    {
        var toolchain = pod.Status?.ContainerStatuses?.FirstOrDefault(s => s.Name == "toolchain");
        return toolchain?.Ready == true;
    }

    private async Task TryDeleteAsync(string podName, string ns)
    {
        try
        {
            await client.CoreV1.DeleteNamespacedPodAsync(podName, ns, gracePeriodSeconds: 0);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete sandbox pod {Pod} after timeout", podName);
        }
    }
}
