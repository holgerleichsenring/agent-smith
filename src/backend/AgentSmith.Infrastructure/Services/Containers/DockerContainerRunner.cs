using System.Diagnostics;
using System.Text;
using AgentSmith.Contracts.Providers;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Containers;

/// <summary>
/// Runs short-lived containers via Docker Engine API (Docker.DotNet).
/// Works locally, in Docker Compose (with socket mount), and anywhere
/// Docker is available. For K8s, use K8sContainerRunner instead.
/// </summary>
public sealed class DockerContainerRunner(
    ILogger<DockerContainerRunner> logger) : IContainerRunner
{
    public async Task<ContainerResult> RunAsync(
        ContainerRunRequest request, CancellationToken cancellationToken)
    {
        using var client = new DockerClientConfiguration().CreateClient();
        var sw = Stopwatch.StartNew();
        var containerName = $"agentsmith-tool-{Guid.NewGuid():N}"[..24];

        var binds = request.VolumeMounts?.Select(kv => $"{kv.Key}:{kv.Value}").ToList()
                    ?? new List<string>();

        var extraHosts = request.ExtraHosts?.Select(kv => $"{kv.Key}:{kv.Value}").ToList()
                         ?? new List<string>();

        var createParams = new CreateContainerParameters
        {
            Name = containerName,
            Image = request.Image,
            Cmd = request.Command.ToList(),
            HostConfig = new HostConfig
            {
                AutoRemove = false,
                Binds = binds,
                ExtraHosts = extraHosts,
            }
        };

        logger.LogDebug("Creating container {Name}: {Image} {Cmd}",
            containerName, request.Image, string.Join(" ", request.Command));

        try
        {
            await PullImageIfMissing(client, request.Image, cancellationToken);

            var response = await client.Containers.CreateContainerAsync(
                createParams, cancellationToken);

            await client.Containers.StartContainerAsync(
                response.ID, new ContainerStartParameters(), cancellationToken);

            // Stream stderr to console in background so user sees Nuclei progress
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));

            var stderrTask = StreamLogsToConsoleAsync(client, response.ID, timeoutCts.Token);

            var waitResponse = await client.Containers.WaitContainerAsync(
                response.ID, timeoutCts.Token);

            // Give the log stream a moment to flush
            try { await stderrTask.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None); }
            catch (TimeoutException) { /* best effort */ }

            var stdout = await ReadContainerLogs(client, response.ID, true, CancellationToken.None);
            var stderr = "";

            sw.Stop();

            logger.LogInformation(
                "Container {Name} exited with code {Code} in {Duration}s",
                containerName, waitResponse.StatusCode, (int)sw.Elapsed.TotalSeconds);

            await client.Containers.RemoveContainerAsync(
                response.ID, new ContainerRemoveParameters(), CancellationToken.None);

            return new ContainerResult(stdout, stderr, (int)waitResponse.StatusCode, (int)sw.Elapsed.TotalSeconds);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Container {Name} timed out after {Timeout}s", containerName, request.TimeoutSeconds);
            await TryRemoveContainer(client, containerName);
            return new ContainerResult("", "Timeout", 1, request.TimeoutSeconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Container {Name} failed", containerName);
            await TryRemoveContainer(client, containerName);
            return new ContainerResult("", ex.Message, 1, (int)sw.Elapsed.TotalSeconds);
        }
    }

    private async Task StreamLogsToConsoleAsync(
        DockerClient client, string containerId, CancellationToken ct)
    {
        try
        {
            var logStream = await client.Containers.GetContainerLogsAsync(
                containerId,
                false,
                new ContainerLogsParameters { ShowStderr = true, Follow = true },
                ct);

            using var stderrPipe = new LogStreamWriter(logger);
            await logStream.CopyOutputToAsync(Stream.Null, Stream.Null, stderrPipe, ct);
        }
        catch (OperationCanceledException) { /* expected on container exit or timeout */ }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Log streaming stopped");
        }
    }

    private static async Task<string> ReadContainerLogs(
        DockerClient client, string containerId, bool stdout, CancellationToken ct)
    {
        var logStream = await client.Containers.GetContainerLogsAsync(
            containerId,
            false,
            new ContainerLogsParameters { ShowStdout = stdout, ShowStderr = !stdout },
            ct);

        using var stdoutMs = new MemoryStream();
        using var stderrMs = new MemoryStream();
        await logStream.CopyOutputToAsync(
            Stream.Null, stdoutMs, stderrMs, ct);

        var ms = stdout ? stdoutMs : stderrMs;

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>
    /// Stream adapter that writes each line to ILogger as it arrives.
    /// </summary>
    private sealed class LogStreamWriter(ILogger logger) : Stream
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
                    logger.LogInformation("[nuclei] {Line}", line);
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
}
