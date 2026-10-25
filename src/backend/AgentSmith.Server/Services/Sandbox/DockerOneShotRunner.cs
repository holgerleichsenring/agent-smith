using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// Runs a short-lived container to completion and removes it: the agent loader and the
/// ownership container (2026-10-25-c5a7b). The output is read only when the exit is non-zero,
/// so a failure can be explained.
/// </summary>
public sealed class DockerOneShotRunner(IDockerClient docker, ILogger<DockerOneShotRunner> logger)
{
    public async Task<DockerOneShotExit> RunAsync(CreateContainerParameters spec, CancellationToken ct)
    {
        var created = await docker.Containers.CreateContainerAsync(spec, ct);
        await docker.Containers.StartContainerAsync(created.ID, new ContainerStartParameters(), ct);
        var wait = await docker.Containers.WaitContainerAsync(created.ID, ct);
        var logs = wait.StatusCode == 0 ? string.Empty : await ReadLogsAsync(created.ID, ct);
        await docker.Containers.RemoveContainerAsync(created.ID, new ContainerRemoveParameters { Force = true }, ct);
        return new DockerOneShotExit(wait.StatusCode, logs);
    }

    private async Task<string> ReadLogsAsync(string containerId, CancellationToken ct)
    {
        try
        {
            using var stream = await docker.Containers.GetContainerLogsAsync(
                containerId, tty: false,
                new ContainerLogsParameters { ShowStdout = true, ShowStderr = true, Tail = "200" }, ct);
            var (stdout, stderr) = await stream.ReadOutputToEndAsync(ct);
            return string.Join("\n", new[] { stdout, stderr }.Where(s => !string.IsNullOrWhiteSpace(s)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Could not read the logs of container {Id}", containerId);
            return string.Empty;
        }
    }
}
