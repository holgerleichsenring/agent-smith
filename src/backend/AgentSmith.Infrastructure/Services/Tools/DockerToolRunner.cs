using System.Diagnostics;
using System.Text;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Tools;

/// <summary>
/// Runs tools as Docker containers via the Docker Engine API.
/// Files are transferred via docker cp — no volume mounts needed.
/// </summary>
public sealed class DockerToolRunner(
    ToolRunnerConfig config,
    ILogger<DockerToolRunner> logger) : IToolRunner
{
    private const string DefaultWorkDir = "/tmp";

    public async Task<ToolResult> RunAsync(ToolRunRequest request, CancellationToken cancellationToken)
    {
        var workDir = request.WorkDir ?? DefaultWorkDir;
        var image = config.Images.GetValueOrDefault(request.Tool, request.Tool);
        var resolvedArgs = ResolveArguments(request.Arguments, workDir);
        var socketUri = config.Socket is not null
            ? new Uri(config.Socket) : new DockerClientConfiguration().EndpointBaseUri;

        using var client = new DockerClientConfiguration(socketUri).CreateClient();
        var sw = Stopwatch.StartNew();
        var containerName = $"agentsmith-tool-{Guid.NewGuid():N}"[..24];

        try
        {
            return await RunContainerAsync(client, containerName, image, resolvedArgs,
                request, workDir, sw, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            var output = await TryCopyOutput(client, containerName, workDir, request.OutputFileName);
            await TryRemove(client, containerName);
            return new ToolResult("", "Timeout", output, 1, request.TimeoutSeconds);
        }
        catch (Exception ex)
        {
            await TryRemove(client, containerName);
            return new ToolResult("", ex.Message, null, 1, (int)sw.Elapsed.TotalSeconds);
        }
    }

    private async Task<ToolResult> RunContainerAsync(
        DockerClient client, string containerName, string image,
        List<string> args, ToolRunRequest request, string workDir,
        Stopwatch sw, CancellationToken ct)
    {
        var extraHosts = request.ExtraHosts?.Select(kv => $"{kv.Key}:{kv.Value}").ToList() ?? [];
        await PullImageIfMissing(client, image, ct);

        var response = await client.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Name = containerName, Image = image, Cmd = args,
            HostConfig = new HostConfig { AutoRemove = false, ExtraHosts = extraHosts }
        }, ct);

        if (request.InputFiles is { Count: > 0 })
            await DockerFileTransfer.CopyToContainerAsync(client, response.ID, request.InputFiles, workDir, ct);

        await client.Containers.StartContainerAsync(response.ID, new ContainerStartParameters(), ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));

        var stderrTask = StreamStderrAsync(client, response.ID, request.Tool, timeoutCts.Token);
        var waitResponse = await client.Containers.WaitContainerAsync(response.ID, timeoutCts.Token);
        try { await stderrTask.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None); }
        catch (TimeoutException) { /* best effort */ }

        var stdout = await ReadStdout(client, response.ID);
        var output = await TryCopyOutput(client, response.ID, workDir, request.OutputFileName);
        sw.Stop();

        await client.Containers.RemoveContainerAsync(response.ID, new ContainerRemoveParameters(), CancellationToken.None);
        return new ToolResult(stdout, "", output, (int)waitResponse.StatusCode, (int)sw.Elapsed.TotalSeconds);
    }

    internal static List<string> ResolveArguments(IReadOnlyList<string> arguments, string workDir) =>
        arguments.Select(a => a.Replace("{work}", workDir)).ToList();

    private static Task<string?> TryCopyOutput(
        DockerClient client, string containerId, string workDir, string? outputFileName) =>
        outputFileName is not null
            ? DockerFileTransfer.CopyFromContainerAsync(client, containerId, $"{workDir}/{outputFileName}")
            : Task.FromResult<string?>(null);

    private async Task StreamStderrAsync(DockerClient client, string containerId, string toolName, CancellationToken ct)
    {
        try
        {
            var logStream = await client.Containers.GetContainerLogsAsync(
                containerId, false, new ContainerLogsParameters { ShowStderr = true, Follow = true }, ct);
            using var pipe = new DockerLogStreamWriter(logger, toolName);
            await logStream.CopyOutputToAsync(Stream.Null, Stream.Null, pipe, ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { logger.LogDebug(ex, "Log streaming stopped"); }
    }

    private static async Task<string> ReadStdout(DockerClient client, string containerId)
    {
        var logStream = await client.Containers.GetContainerLogsAsync(
            containerId, false, new ContainerLogsParameters { ShowStdout = true, ShowStderr = false }, CancellationToken.None);
        using var ms = new MemoryStream();
        await logStream.CopyOutputToAsync(Stream.Null, ms, Stream.Null, CancellationToken.None);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static async Task PullImageIfMissing(DockerClient client, string image, CancellationToken ct)
    {
        try { await client.Images.InspectImageAsync(image, ct); }
        catch (DockerImageNotFoundException)
        {
            var parts = image.Split(':');
            await client.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = parts[0], Tag = parts.Length > 1 ? parts[1] : "latest" },
                null, new Progress<JSONMessage>(), ct);
        }
    }

    private static async Task TryRemove(DockerClient client, string name)
    {
        try { await client.Containers.RemoveContainerAsync(name, new ContainerRemoveParameters { Force = true }); }
        catch { /* best effort */ }
    }
}
