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
