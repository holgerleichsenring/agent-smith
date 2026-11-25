using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using k8s;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// ISandbox implementation backed by one toolchain container of a Kubernetes pod. Communication runs over
/// SandboxRedisChannel. 2026-11-25-9992b: a single sandbox OWNS its pod and deletes it on DisposeAsync; a container of
/// a run's workspace does not — <see cref="ContainerWorkspace"/> deletes the shared pod once, after every container.
/// </summary>
public sealed class KubernetesSandbox(
    IKubernetes client,
    KubernetesContainerAddress address,
    string jobId,
    SandboxRedisChannel channel,
    SandboxBusCredential bus,
    int stepTimeoutCapSeconds,
    string toolchainImage,
    ResolvedSandboxSecrets injectedSecrets,
    bool ownsPod,
    ILogger logger) : IHoldableSandbox, ISandboxToolchainImage, ISandboxSecretInjection, ISandboxExitReader, ISandboxWorkDirectory
{
    /// <summary>2026-11-25-9992c: the pod holds one tree at the workspace mount; a run repository's RepoSandbox names its own.</summary>
    public string WorkDirectory => Domain.Entities.Repository.WorkspaceRoot;

    private static readonly TimeSpan ShutdownGrace = TimeSpan.FromSeconds(10);
    private readonly KubernetesPodExitReader _exits = new(client);

    public string JobId => jobId;

    // 2026-08-31-7097: the image this pod's toolchain container was created from.
    public string ToolchainImage => toolchainImage;

    // 2026-08-28-b630: the declared credentials the pod spec projected into this pod. The
    // pod builder is their only consumer, so this backend is the only one that names them.
    public ResolvedSandboxSecrets InjectedSecrets => injectedSecrets;

    // 2026-10-13-9a59i: read before the delete — the pod (RestartPolicy Never) still holds the exit.
    // 2026-11-25-9992b: this container's own exit, not a sibling's.
    public Task<SandboxExitRead> ReadExitAsync(CancellationToken cancellationToken) =>
        _exits.ReadAsync(address.PodName, address.Container, address.Namespace, cancellationToken);

    public async Task<StepResult> RunStepAsync(
        Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        // p0200/p0407: same contract as the Docker backend — the agent enforces the cap
        // on the clamped step and reports why it killed the command; the host wait only
        // covers a sandbox that has gone silent.
        var capped = SandboxStepCap.Clamp(step, stepTimeoutCapSeconds);
        return await channel.RunStepAsync(capped, progress, SandboxStepCap.ChannelWait(capped), cancellationToken);
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
            logger.LogWarning(ex, "Sandbox shutdown signal failed for {Container} of pod {Pod}",
                address.Container, address.PodName);
        }
        await ForceRemoveAsync(CancellationToken.None);
    }

    /// <summary>
    /// 2026-09-22-2d11a: the same teardown without the shutdown step and its flat
    /// ten-second wait — what releasing a held sandbox in front of a capacity probe does.
    /// </summary>
    public async Task ForceRemoveAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        // p0355: the pod delete must run on EVERY terminal path, even if the channel
        // teardown throws — a leaked channel is cheap, a leaked pod holds the quota.
        try { await channel.DisposeAsync(); }
        catch (Exception ex) { logger.LogWarning(ex, "Sandbox channel dispose failed for pod {Pod}", address.PodName); }
        await bus.DisposeAsync(); // 2026-10-25-c5a7e: after the channel, which still used the user
        if (ownsPod) await new KubernetesPodRemoval(client, logger).DeleteAsync(address);
    }
}
