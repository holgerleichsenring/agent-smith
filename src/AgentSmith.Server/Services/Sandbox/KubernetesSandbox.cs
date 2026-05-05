using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using k8s;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// ISandbox implementation backed by a Kubernetes Pod. Communication runs over
/// SandboxRedisChannel; pod cleanup happens on DisposeAsync.
/// </summary>
public sealed class KubernetesSandbox(
    IKubernetes client,
    string @namespace,
    string podName,
    string jobId,
    SandboxRedisChannel channel,
    ILogger logger) : ISandbox
{
    private static readonly TimeSpan ShutdownGrace = TimeSpan.FromSeconds(10);

    public string JobId => jobId;

    public async Task<StepResult> RunStepAsync(
        Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        await channel.PushStepAsync(step, cancellationToken);
        var timeout = TimeSpan.FromSeconds(step.TimeoutSeconds + 30);
        return await channel.WaitForResultAsync(step.StepId, progress, timeout, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await channel.PushStepAsync(Step.Shutdown(Guid.NewGuid()), CancellationToken.None);
            await Task.Delay(ShutdownGrace);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Sandbox shutdown signal failed for pod {Pod}", podName);
        }
        await channel.DisposeAsync();
        await TryDeletePodAsync();
    }

    private async Task TryDeletePodAsync()
    {
        try
        {
            await client.CoreV1.DeleteNamespacedPodAsync(podName, @namespace, gracePeriodSeconds: 0);
            logger.LogInformation("Sandbox pod {Pod} deleted", podName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete sandbox pod {Pod}", podName);
        }
    }
}
