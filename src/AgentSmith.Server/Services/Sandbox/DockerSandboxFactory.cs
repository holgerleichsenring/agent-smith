using AgentSmith.Contracts.Sandbox;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// Creates the agent-loader + toolchain container pair on Docker, sharing two
/// named volumes (shared for the agent binary, work for the source tree).
/// </summary>
public sealed class DockerSandboxFactory(
    IDockerClient docker,
    IConnectionMultiplexer redis,
    DockerContainerSpecBuilder specBuilder,
    DockerSandboxOptions options,
    ILoggerFactory loggerFactory) : ISandboxFactory
{
    public async Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var slug = jobId[..12];
        var sharedVolume = $"agentsmith-sandbox-{slug}-shared";
        var workVolume = $"agentsmith-sandbox-{slug}-work";

        await CreateVolumesAsync(sharedVolume, workVolume, cancellationToken);
        await RunLoaderAsync(slug, sharedVolume, spec.AgentImage, cancellationToken);
        var toolchainId = await StartToolchainAsync(slug, sharedVolume, workVolume, jobId, spec, cancellationToken);

        var channel = new SandboxRedisChannel(redis, jobId, loggerFactory.CreateLogger<SandboxRedisChannel>());
        return new DockerSandbox(docker, toolchainId, sharedVolume, workVolume, jobId, channel,
            loggerFactory.CreateLogger<DockerSandbox>());
    }

    private async Task CreateVolumesAsync(string shared, string work, CancellationToken ct)
    {
        await docker.Volumes.CreateAsync(new VolumesCreateParameters { Name = shared }, ct);
        await docker.Volumes.CreateAsync(new VolumesCreateParameters { Name = work }, ct);
    }

    private async Task RunLoaderAsync(string slug, string sharedVolume, string agentImage, CancellationToken ct)
    {
        await EnsureImagePresentAsync(agentImage, isCarrier: true, ct);
        var spec = specBuilder.BuildLoader($"agentsmith-sandbox-loader-{slug}", sharedVolume, agentImage);
        var created = await docker.Containers.CreateContainerAsync(spec, ct);
        await docker.Containers.StartContainerAsync(created.ID, new ContainerStartParameters(), ct);
        var wait = await docker.Containers.WaitContainerAsync(created.ID, ct);

        if (wait.StatusCode != 0)
        {
            var logs = await ReadContainerLogsAsync(created.ID, ct);
            await docker.Containers.RemoveContainerAsync(created.ID,
                new ContainerRemoveParameters { Force = true }, ct);
            throw new InvalidOperationException(
                $"Sandbox-agent loader exited with code {wait.StatusCode} — `{agentImage}` failed " +
                $"to inject /shared/agent. Without the binary in place the toolchain crashes with " +
                $"the misleading 'exec /shared/agent: no such file or directory'. Loader output: " +
                $"{(string.IsNullOrWhiteSpace(logs) ? "<empty>" : logs.Trim())}");
        }

        await docker.Containers.RemoveContainerAsync(created.ID,
            new ContainerRemoveParameters { Force = true }, ct);
    }

    private async Task<string> ReadContainerLogsAsync(string containerId, CancellationToken ct)
    {
        try
        {
            using var stream = await docker.Containers.GetContainerLogsAsync(
                containerId,
                tty: false,
                new ContainerLogsParameters { ShowStdout = true, ShowStderr = true, Tail = "200" },
                ct);
            var (stdout, stderr) = await stream.ReadOutputToEndAsync(ct);
            return string.Join("\n", new[] { stdout, stderr }.Where(s => !string.IsNullOrWhiteSpace(s)));
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task<string> StartToolchainAsync(
        string slug, string sharedVolume, string workVolume, string jobId,
        SandboxSpec spec, CancellationToken ct)
    {
        await EnsureImagePresentAsync(spec.ToolchainImage, isCarrier: false, ct);
        var containerSpec = specBuilder.BuildToolchain(
            $"agentsmith-sandbox-{slug}", sharedVolume, workVolume, jobId, options.RedisUrl, spec);
        var created = await docker.Containers.CreateContainerAsync(containerSpec, ct);
        await docker.Containers.StartContainerAsync(created.ID, new ContainerStartParameters(), ct);
        loggerFactory.CreateLogger<DockerSandboxFactory>()
            .LogInformation("Sandbox container {Id} started for job {JobId}", created.ID, jobId);
        return created.ID;
    }

    // Honors IfNotPresent semantic universally. Toolchain images (alpine, node,
    // python, dotnet/sdk, …) are pulled from Docker Hub on demand; the carrier
    // agent image is typically locally-built, so pull failures fall back to a
    // helpful "build it first" message.
    private async Task EnsureImagePresentAsync(string image, bool isCarrier, CancellationToken ct)
    {
        try
        {
            await docker.Images.InspectImageAsync(image, ct);
            return;
        }
        catch (DockerImageNotFoundException) { /* fall through to pull */ }

        var logger = loggerFactory.CreateLogger<DockerSandboxFactory>();
        var (repo, tag) = SplitImageRef(image);
        logger.LogInformation("Pulling image {Image} (not present locally)", image);
        try
        {
            await docker.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = repo, Tag = tag },
                authConfig: null, new Progress<JSONMessage>(), ct);
        }
        catch (DockerApiException ex) when (isCarrier)
        {
            throw new InvalidOperationException(
                $"Sandbox agent image '{image}' not found locally and not pullable from a registry. " +
                $"Build it once with: docker compose --profile build-only build sandbox-agent " +
                $"(or: docker build -t {image} -f src/AgentSmith.Sandbox.Agent/Dockerfile .)", ex);
        }
    }

    private static (string Repo, string Tag) SplitImageRef(string image)
    {
        var lastColon = image.LastIndexOf(':');
        // Guard against `host:port/repo` references where the colon belongs to
        // the registry, not a tag.
        if (lastColon < 0 || image.IndexOf('/', lastColon) >= 0)
            return (image, "latest");
        return (image[..lastColon], image[(lastColon + 1)..]);
    }
}
