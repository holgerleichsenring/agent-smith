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
/// Uses docker cp for file transfer — no volume mounts, no shared temp directories.
/// Works identically on macOS, Linux, Windows, Docker Compose, and CI/CD.
/// </summary>
public sealed class DockerToolRunner(
    ToolRunnerConfig config,
    ILogger<DockerToolRunner> logger) : IToolRunner
{
    public async Task<ToolResult> RunAsync(
        ToolRunRequest request, CancellationToken cancellationToken)
    {
        var image = ResolveImage(request.Tool);

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

            // Copy input files into the container via docker cp (no volume mounts needed)
            // Target is /tmp — guaranteed to exist and be writable in all container images
            if (request.InputFiles is { Count: > 0 })
                await CopyFilesToContainerAsync(client, response.ID, "/tmp", request.InputFiles, cancellationToken);

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

            // Copy output file from container via docker cp
            string? outputContent = null;
            if (request.OutputFileName is not null)
                outputContent = await CopyFileFromContainerAsync(
                    client, response.ID, $"/tmp/{request.OutputFileName}");

            await client.Containers.RemoveContainerAsync(
                response.ID, new ContainerRemoveParameters(), CancellationToken.None);

            return new ToolResult(stdout, "", outputContent,
                (int)waitResponse.StatusCode, (int)sw.Elapsed.TotalSeconds);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Container {Name} timed out after {Timeout}s",
                containerName, request.TimeoutSeconds);

            string? outputContent = null;
            if (request.OutputFileName is not null)
                outputContent = await CopyFileFromContainerAsync(
                    client, containerName, $"/tmp/{request.OutputFileName}");

            await TryRemoveContainer(client, containerName);
            return new ToolResult("", "Timeout", outputContent, 1, request.TimeoutSeconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Container {Name} failed", containerName);
            await TryRemoveContainer(client, containerName);
            return new ToolResult("", ex.Message, null, 1, (int)sw.Elapsed.TotalSeconds);
        }
    }

    private string ResolveImage(string tool) =>
        config.Images.TryGetValue(tool, out var image) ? image : tool;

    /// <summary>
    /// Copies files into a container using the Docker API (equivalent to docker cp).
    /// Builds a minimal tar archive in memory — no external dependencies needed.
    /// </summary>
    internal static async Task CopyFilesToContainerAsync(
        DockerClient client, string containerId, string targetPath,
        Dictionary<string, string> files, CancellationToken ct)
    {
        using var tarStream = new MemoryStream();
        foreach (var (fileName, content) in files)
        {
            var contentBytes = Encoding.UTF8.GetBytes(content);
            WriteTarEntry(tarStream, fileName, contentBytes);
        }

        // Write two 512-byte zero blocks to mark end of tar archive
        tarStream.Write(new byte[1024]);
        tarStream.Position = 0;

        await client.Containers.ExtractArchiveToContainerAsync(
            containerId,
            new ContainerPathStatParameters { Path = targetPath },
            tarStream, ct);
    }

    private static void WriteTarEntry(Stream stream, string fileName, byte[] content)
    {
        var header = new byte[512];

        // File name (offset 0, 100 bytes)
        var nameBytes = Encoding.ASCII.GetBytes(fileName);
        Array.Copy(nameBytes, 0, header, 0, Math.Min(nameBytes.Length, 100));

        // File mode (offset 100, 8 bytes) — 0644
        Encoding.ASCII.GetBytes("0000644\0").CopyTo(header, 100);

        // Owner/group ID (offset 108/116, 8 bytes each) — 0
        Encoding.ASCII.GetBytes("0000000\0").CopyTo(header, 108);
        Encoding.ASCII.GetBytes("0000000\0").CopyTo(header, 116);

        // File size in octal (offset 124, 12 bytes)
        Encoding.ASCII.GetBytes(Convert.ToString(content.Length, 8).PadLeft(11, '0') + "\0").CopyTo(header, 124);

        // Modification time (offset 136, 12 bytes) — current time
        var unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Encoding.ASCII.GetBytes(Convert.ToString(unixTime, 8).PadLeft(11, '0') + "\0").CopyTo(header, 136);

        // Type flag (offset 156) — '0' = regular file
        header[156] = (byte)'0';

        // USTAR magic (offset 257)
        Encoding.ASCII.GetBytes("ustar\0").CopyTo(header, 257);
        Encoding.ASCII.GetBytes("00").CopyTo(header, 263);

        // Compute checksum (offset 148, 8 bytes) — sum of all header bytes with checksum field as spaces
        Encoding.ASCII.GetBytes("        ").CopyTo(header, 148); // 8 spaces
        var checksum = 0;
        for (var i = 0; i < 512; i++) checksum += header[i];
        Encoding.ASCII.GetBytes(Convert.ToString(checksum, 8).PadLeft(6, '0') + "\0 ").CopyTo(header, 148);

        stream.Write(header);
        stream.Write(content);

        // Pad to 512-byte boundary
        var padding = 512 - (content.Length % 512);
        if (padding < 512)
            stream.Write(new byte[padding]);
    }

    /// <summary>
    /// Copies a single file from a container using the Docker API (equivalent to docker cp).
    /// </summary>
    private static async Task<string?> CopyFileFromContainerAsync(
        DockerClient client, string containerId, string containerPath)
    {
        try
        {
            var response = await client.Containers.GetArchiveFromContainerAsync(
                containerId,
                new GetArchiveFromContainerParameters { Path = containerPath },
                false, CancellationToken.None);

            // Read tar archive — skip 512-byte header, read content
            using var ms = new MemoryStream();
            await response.Stream.CopyToAsync(ms);
            ms.Position = 0;

            var header = new byte[512];
            if (await ms.ReadAsync(header) < 512)
                return null;

            // Parse file size from octal (offset 124, 12 bytes)
            var sizeStr = Encoding.ASCII.GetString(header, 124, 11).Trim('\0', ' ');
            if (!TryParseOctal(sizeStr, out var size) || size == 0)
                return null;

            var content = new byte[size];
            var read = await ms.ReadAsync(content.AsMemory(0, (int)size));
            return Encoding.UTF8.GetString(content, 0, read);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryParseOctal(string s, out long result)
    {
        result = 0;
        try { result = Convert.ToInt64(s, 8); return true; }
        catch { return false; }
    }

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
