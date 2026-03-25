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
/// {work} in arguments is resolved to /work inside the container.
/// </summary>
public sealed class DockerToolRunner(
    ToolRunnerConfig config,
    ILogger<DockerToolRunner> logger) : IToolRunner
{
    private const string WorkDir = "/tmp";

    public async Task<ToolResult> RunAsync(
        ToolRunRequest request, CancellationToken cancellationToken)
    {
        var image = config.Images.GetValueOrDefault(request.Tool, request.Tool);
        var resolvedArgs = ResolveArguments(request.Arguments, WorkDir);

        var socketUri = config.Socket is not null
            ? new Uri(config.Socket)
            : new DockerClientConfiguration().EndpointBaseUri;

        using var client = new DockerClientConfiguration(socketUri).CreateClient();
        var sw = Stopwatch.StartNew();
        var containerName = $"agentsmith-tool-{Guid.NewGuid():N}"[..24];

        var extraHosts = request.ExtraHosts?.Select(kv => $"{kv.Key}:{kv.Value}").ToList()
                         ?? [];

        var createParams = new CreateContainerParameters
        {
            Name = containerName,
            Image = image,
            Cmd = resolvedArgs,
            HostConfig = new HostConfig
            {
                AutoRemove = false,
                ExtraHosts = extraHosts,
            }
        };

        logger.LogDebug("Creating container {Name}: {Image} {Cmd}",
            containerName, image, string.Join(" ", resolvedArgs));

        try
        {
            await PullImageIfMissing(client, image, cancellationToken);

            var response = await client.Containers.CreateContainerAsync(
                createParams, cancellationToken);

            if (request.InputFiles is { Count: > 0 })
                await CopyFilesToContainer(client, response.ID, request.InputFiles, cancellationToken);

            await client.Containers.StartContainerAsync(
                response.ID, new ContainerStartParameters(), cancellationToken);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));

            var stderrTask = StreamStderrAsync(client, response.ID, request.Tool, timeoutCts.Token);
            var waitResponse = await client.Containers.WaitContainerAsync(response.ID, timeoutCts.Token);

            try { await stderrTask.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None); }
            catch (TimeoutException) { /* best effort flush */ }

            var stdout = await ReadStdout(client, response.ID);
            var outputContent = request.OutputFileName is not null
                ? await CopyFileFromContainer(client, response.ID, $"{WorkDir}/{request.OutputFileName}")
                : null;

            sw.Stop();
            logger.LogInformation("Container {Name} exited with code {Code} in {Duration}s",
                containerName, waitResponse.StatusCode, (int)sw.Elapsed.TotalSeconds);

            await client.Containers.RemoveContainerAsync(
                response.ID, new ContainerRemoveParameters(), CancellationToken.None);

            return new ToolResult(stdout, "", outputContent,
                (int)waitResponse.StatusCode, (int)sw.Elapsed.TotalSeconds);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Container {Name} timed out after {Timeout}s",
                containerName, request.TimeoutSeconds);

            var outputContent = request.OutputFileName is not null
                ? await CopyFileFromContainer(client, containerName, $"{WorkDir}/{request.OutputFileName}")
                : null;

            await TryRemove(client, containerName);
            return new ToolResult("", "Timeout", outputContent, 1, request.TimeoutSeconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Container {Name} failed", containerName);
            await TryRemove(client, containerName);
            return new ToolResult("", ex.Message, null, 1, (int)sw.Elapsed.TotalSeconds);
        }
    }

    // --- Placeholder resolution ---

    internal static List<string> ResolveArguments(IReadOnlyList<string> arguments, string workDir) =>
        arguments.Select(a => a.Replace("{work}", workDir)).ToList();

    // --- Docker cp: files in ---

    private static async Task CopyFilesToContainer(
        DockerClient client, string containerId,
        Dictionary<string, string> files, CancellationToken ct)
    {
        using var tar = new MemoryStream();
        foreach (var (name, content) in files)
            WriteTarEntry(tar, name, Encoding.UTF8.GetBytes(content));
        tar.Write(new byte[1024]); // end-of-archive marker
        tar.Position = 0;

        await client.Containers.ExtractArchiveToContainerAsync(
            containerId,
            new ContainerPathStatParameters { Path = WorkDir },
            tar, ct);
    }

    private static void WriteTarEntry(Stream stream, string fileName, byte[] content)
    {
        var header = new byte[512];
        Encoding.ASCII.GetBytes(fileName).AsSpan().CopyTo(header.AsSpan(0, 100));
        Encoding.ASCII.GetBytes("0000644\0").CopyTo(header, 100);   // mode
        Encoding.ASCII.GetBytes("0000000\0").CopyTo(header, 108);   // uid
        Encoding.ASCII.GetBytes("0000000\0").CopyTo(header, 116);   // gid
        Encoding.ASCII.GetBytes(Convert.ToString(content.Length, 8).PadLeft(11, '0') + "\0").CopyTo(header, 124);
        Encoding.ASCII.GetBytes(Convert.ToString(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 8).PadLeft(11, '0') + "\0").CopyTo(header, 136);
        header[156] = (byte)'0'; // regular file
        Encoding.ASCII.GetBytes("ustar\0").CopyTo(header, 257);
        Encoding.ASCII.GetBytes("00").CopyTo(header, 263);

        // checksum
        Encoding.ASCII.GetBytes("        ").CopyTo(header, 148);
        var checksum = 0;
        for (var i = 0; i < 512; i++) checksum += header[i];
        Encoding.ASCII.GetBytes(Convert.ToString(checksum, 8).PadLeft(6, '0') + "\0 ").CopyTo(header, 148);

        stream.Write(header);
        stream.Write(content);

        var padding = 512 - (content.Length % 512);
        if (padding < 512) stream.Write(new byte[padding]);
    }

    // --- Docker cp: files out ---

    private static async Task<string?> CopyFileFromContainer(
        DockerClient client, string containerId, string path)
    {
        try
        {
            var response = await client.Containers.GetArchiveFromContainerAsync(
                containerId,
                new GetArchiveFromContainerParameters { Path = path },
                false, CancellationToken.None);

            using var ms = new MemoryStream();
            await response.Stream.CopyToAsync(ms);
            ms.Position = 0;

            var header = new byte[512];
            if (await ms.ReadAsync(header) < 512) return null;

            var sizeStr = Encoding.ASCII.GetString(header, 124, 11).Trim('\0', ' ');
            if (sizeStr.Length == 0) return null;
            var size = Convert.ToInt64(sizeStr, 8);
            if (size == 0) return null;

            var content = new byte[size];
            var read = await ms.ReadAsync(content.AsMemory(0, (int)size));
            return Encoding.UTF8.GetString(content, 0, read);
        }
        catch { return null; }
    }

    // --- Log streaming ---

    private async Task StreamStderrAsync(
        DockerClient client, string containerId, string toolName, CancellationToken ct)
    {
        try
        {
            var logStream = await client.Containers.GetContainerLogsAsync(
                containerId, false,
                new ContainerLogsParameters { ShowStderr = true, Follow = true }, ct);
            using var pipe = new LogStreamWriter(logger, toolName);
            await logStream.CopyOutputToAsync(Stream.Null, Stream.Null, pipe, ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { logger.LogDebug(ex, "Log streaming stopped"); }
    }

    private static async Task<string> ReadStdout(DockerClient client, string containerId)
    {
        var logStream = await client.Containers.GetContainerLogsAsync(
            containerId, false,
            new ContainerLogsParameters { ShowStdout = true, ShowStderr = false },
            CancellationToken.None);
        using var stdoutMs = new MemoryStream();
        using var stderrMs = new MemoryStream();
        await logStream.CopyOutputToAsync(Stream.Null, stdoutMs, stderrMs, CancellationToken.None);
        return Encoding.UTF8.GetString(stdoutMs.ToArray());
    }

    // --- Image + container management ---

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

    // --- Stderr log adapter ---

    private sealed class LogStreamWriter(ILogger logger, string toolName) : Stream
    {
        private readonly StringBuilder _buffer = new();
        public override void Write(byte[] buffer, int offset, int count)
        {
            _buffer.Append(Encoding.UTF8.GetString(buffer, offset, count));
            while (_buffer.ToString().Contains('\n'))
            {
                var content = _buffer.ToString();
                var idx = content.IndexOf('\n');
                var line = content[..idx].TrimEnd('\r');
                _buffer.Remove(0, idx + 1);
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
