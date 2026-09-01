using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// ISandbox implementation backed by Docker. Communication runs over
/// SandboxRedisChannel; container + volume cleanup happens on DisposeAsync.
/// </summary>
public sealed class DockerSandbox(
    IDockerClient docker,
    string toolchainContainerId,
    string sharedVolume,
    string workVolume,
    string jobId,
    SandboxRedisChannel channel,
    int stepTimeoutCapSeconds,
    string toolchainImage,
    ILogger logger) : ISandbox, ISandboxLivenessProbeTarget, ISandboxToolchainImage
{
    private static readonly TimeSpan ShutdownGrace = TimeSpan.FromSeconds(10);

    public string JobId => jobId;
    public string LivenessProbeTargetId => toolchainContainerId;

    // 2026-08-31-7097: this container really was started from this image, so anything
    // reporting on the tools it carries can name it.
    public string ToolchainImage => toolchainImage;

    public async Task<StepResult> RunStepAsync(
        Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        // p0200/p0407: the agent runs the CLAMPED step, so a command that outlives the
        // operator's cap is killed by the agent — which reports the timeout with the
        // output it captured. The host wait is the backstop for a silent sandbox.
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
            logger.LogWarning(ex, "Sandbox shutdown signal failed for container {Id}", toolchainContainerId);
        }
        await channel.DisposeAsync();
        await TryRemoveContainerAsync();
        await TryRemoveVolumeAsync(sharedVolume);
        await TryRemoveVolumeAsync(workVolume);
    }

    private async Task TryRemoveContainerAsync()
    {
        try
        {
            await docker.Containers.RemoveContainerAsync(toolchainContainerId,
                new ContainerRemoveParameters { Force = true });
            logger.LogInformation("Sandbox container {Id} removed", toolchainContainerId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to remove sandbox container {Id}", toolchainContainerId);
        }
    }

    private async Task TryRemoveVolumeAsync(string volumeName)
    {
        try
        {
            await docker.Volumes.RemoveAsync(volumeName, force: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to remove sandbox volume {Name}", volumeName);
        }
    }
}
