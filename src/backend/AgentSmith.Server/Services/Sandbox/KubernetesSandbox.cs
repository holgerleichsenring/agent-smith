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
    int stepTimeoutCapSeconds,
    string toolchainImage,
    ResolvedSandboxSecrets injectedSecrets,
    ILogger logger) : ISandbox, ISandboxToolchainImage, ISandboxSecretInjection
{
    private static readonly TimeSpan ShutdownGrace = TimeSpan.FromSeconds(10);

    public string JobId => jobId;

    // 2026-08-31-7097: the image this pod's toolchain container was created from.
    public string ToolchainImage => toolchainImage;

    // 2026-08-28-b630: the declared credentials the pod spec projected into this pod. The
    // pod builder is their only consumer, so this backend is the only one that names them.
    public ResolvedSandboxSecrets InjectedSecrets => injectedSecrets;

    public async Task<StepResult> RunStepAsync(
        Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        // p0200/p0407: same contract as the Docker backend — the agent enforces the cap
        // on the clamped step and reports why it killed the command; the host wait only
        // covers a sandbox that has gone silent.
        var capped = SandboxStepCap.Clamp(step, stepTimeoutCapSeconds);
        await channel.PushStepAsync(capped, cancellationToken);
        return await channel.WaitForResultAsync(
            capped.StepId, progress, SandboxStepCap.ChannelWait(capped), cancellationToken);
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
        // p0355: the pod delete must run on EVERY terminal path, even if the channel
        // teardown throws — a leaked channel is cheap, a leaked pod holds the quota.
        try { await channel.DisposeAsync(); }
        catch (Exception ex) { logger.LogWarning(ex, "Sandbox channel dispose failed for pod {Pod}", podName); }
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
