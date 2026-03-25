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
/// Handles temp directory management, volume mounts, and output file collection.
/// Works in any environment with Docker socket access (local, Docker Compose, CI/CD).
/// </summary>
public sealed class DockerToolRunner(
    ToolRunnerConfig config,
    ILogger<DockerToolRunner> logger) : IToolRunner
{
    public async Task<ToolResult> RunAsync(
        ToolRunRequest request, CancellationToken cancellationToken)
    {
        var image = ResolveImage(request.Tool);
        var tempDir = Path.Combine(GetSharedTempPath(), $"{request.Tool}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Write input files to temp directory
            if (request.InputFiles is not null)
            {
                foreach (var (fileName, content) in request.InputFiles)
                    await File.WriteAllTextAsync(
                        Path.Combine(tempDir, fileName), content, cancellationToken);
            }

            var socketUri = config.Socket is not null
                ? new Uri(config.Socket)
                : new DockerClientConfiguration().EndpointBaseUri;

            using var client = new DockerClientConfiguration(socketUri).CreateClient();
            var sw = Stopwatch.StartNew();
            var containerName = $"agentsmith-tool-{Guid.NewGuid():N}"[..24];

            var extraHosts = request.ExtraHosts?.Select(kv => $"{kv.Key}:{kv.Value}").ToList()
                             ?? new List<string>();

            var createParams = new CreateContainerParameters
            {
                Name = containerName,
                Image = image,
                Cmd = request.Arguments.ToList(),
                HostConfig = new HostConfig
                {
                    AutoRemove = false,
                    Binds = [$"{tempDir}:/input"],
                    ExtraHosts = extraHosts,
                }
            };

            logger.LogDebug("Creating container {Name}: {Image} {Cmd}",
                containerName, image, string.Join(" ", request.Arguments));

            try
            {
                await PullImageIfMissing(client, image, cancellationToken);

                var response = await client.Containers.CreateContainerAsync(
                    createParams, cancellationToken);

                await client.Containers.StartContainerAsync(
                    response.ID, new ContainerStartParameters(), cancellationToken);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));

                var stderrTask = StreamLogsToConsoleAsync(client, response.ID, request.Tool, timeoutCts.Token);

                var waitResponse = await client.Containers.WaitContainerAsync(
                    response.ID, timeoutCts.Token);

                try { await stderrTask.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None); }
                catch (TimeoutException) { /* best effort */ }

                var stdout = await ReadContainerLogs(client, response.ID, CancellationToken.None);
                sw.Stop();

                logger.LogInformation(
                    "Container {Name} exited with code {Code} in {Duration}s",
                    containerName, waitResponse.StatusCode, (int)sw.Elapsed.TotalSeconds);

                await client.Containers.RemoveContainerAsync(
                    response.ID, new ContainerRemoveParameters(), CancellationToken.None);

                // Read output file if requested
                string? outputContent = null;
                if (request.OutputFileName is not null)
                {
                    var outputPath = Path.Combine(tempDir, request.OutputFileName);
                    if (File.Exists(outputPath))
                        outputContent = await File.ReadAllTextAsync(outputPath, CancellationToken.None);
                }

                return new ToolResult(stdout, "", outputContent,
                    (int)waitResponse.StatusCode, (int)sw.Elapsed.TotalSeconds);
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Container {Name} timed out after {Timeout}s",
                    containerName, request.TimeoutSeconds);
                await TryRemoveContainer(client, containerName);

                // Try to read output file even on timeout
                string? outputContent = null;
                if (request.OutputFileName is not null)
                {
                    var outputPath = Path.Combine(tempDir, request.OutputFileName);
                    if (File.Exists(outputPath))
                        outputContent = await File.ReadAllTextAsync(outputPath, CancellationToken.None);
                }

                return new ToolResult("", "Timeout", outputContent, 1, request.TimeoutSeconds);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Container {Name} failed", containerName);
                await TryRemoveContainer(client, containerName);
                return new ToolResult("", ex.Message, null, 1, (int)sw.Elapsed.TotalSeconds);
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    private string ResolveImage(string tool) =>
        config.Images.TryGetValue(tool, out var image) ? image : tool;

    private async Task StreamLogsToConsoleAsync(
        DockerClient client, string containerId, string toolName, CancellationToken ct)
    {
        try
        {
            var logStream = await client.Containers.GetContainerLogsAsync(
                containerId, false,
                new ContainerLogsParameters { ShowStderr = true, Follow = true }, ct);

            using var stderrPipe = new LogStreamWriter(logger, toolName);
            await logStream.CopyOutputToAsync(Stream.Null, Stream.Null, stderrPipe, ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { logger.LogDebug(ex, "Log streaming stopped"); }
    }

    private static async Task<string> ReadContainerLogs(
        DockerClient client, string containerId, CancellationToken ct)
    {
        var logStream = await client.Containers.GetContainerLogsAsync(
            containerId, false,
            new ContainerLogsParameters { ShowStdout = true, ShowStderr = false }, ct);

        using var stdoutMs = new MemoryStream();
        using var stderrMs = new MemoryStream();
        await logStream.CopyOutputToAsync(Stream.Null, stdoutMs, stderrMs, ct);

        return Encoding.UTF8.GetString(stdoutMs.ToArray());
    }

    private static async Task PullImageIfMissing(
        DockerClient client, string image, CancellationToken ct)
    {
        try
        {
            await client.Images.InspectImageAsync(image, ct);
        }
        catch (DockerImageNotFoundException)
        {
            var parts = image.Split(':');
            var repo = parts[0];
            var tag = parts.Length > 1 ? parts[1] : "latest";
            await client.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = repo, Tag = tag },
                null, new Progress<JSONMessage>(), ct);
        }
    }

    private static async Task TryRemoveContainer(DockerClient client, string name)
    {
        try
        {
            await client.Containers.RemoveContainerAsync(
                name, new ContainerRemoveParameters { Force = true });
        }
        catch { /* best effort */ }
    }

    internal static string GetSharedTempPath()
    {
        const string sharedPath = "/tmp/agentsmith";
        if (Directory.Exists(sharedPath))
            return sharedPath;

        return Path.GetTempPath();
    }

    private sealed class LogStreamWriter(ILogger logger, string toolName) : Stream
    {
        private readonly StringBuilder _buffer = new();

        public override void Write(byte[] buffer, int offset, int count)
        {
            var text = Encoding.UTF8.GetString(buffer, offset, count);
            _buffer.Append(text);

            while (_buffer.ToString().Contains('\n'))
            {
                var content = _buffer.ToString();
                var newlineIndex = content.IndexOf('\n');
                var line = content[..newlineIndex].TrimEnd('\r');
                _buffer.Remove(0, newlineIndex + 1);

                if (!string.IsNullOrWhiteSpace(line))
                    logger.LogInformation("[{Tool}] {Line}", toolName, line);
            }
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get => 0; set { } }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => 0;
        public override long Seek(long offset, SeekOrigin origin) => 0;
        public override void SetLength(long value) { }
    }
}
