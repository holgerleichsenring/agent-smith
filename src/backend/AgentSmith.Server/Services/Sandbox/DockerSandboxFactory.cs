using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    IOptions<SandboxGlobalConfig> sandboxConfig,
    ILoggerFactory loggerFactory) : ISandboxFactory
{
    public async Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var slug = jobId[..12];
        var sharedVolume = $"agentsmith-sandbox-{slug}-shared";
        var workVolume = $"agentsmith-sandbox-{slug}-work";

        var network = await ResolveNetworkAsync(cancellationToken);

        await CreateVolumesAsync(sharedVolume, workVolume, cancellationToken);
        await RunLoaderAsync(slug, sharedVolume, spec.AgentImage, network, cancellationToken);
        var toolchainId = await StartToolchainAsync(slug, sharedVolume, workVolume, jobId, spec, network, cancellationToken);

        var channel = new SandboxRedisChannel(redis, jobId, loggerFactory.CreateLogger<SandboxRedisChannel>());
        return new DockerSandbox(docker, toolchainId, sharedVolume, workVolume, jobId, channel,
            // p0230: per-project resolved cap rides on the spec; fall back to global.
            spec.StepTimeoutSeconds ?? sandboxConfig.Value.StepTimeoutSeconds,
            loggerFactory.CreateLogger<DockerSandbox>());
    }

    // Mirrors DockerNetworkResolver (used by DockerJobSpawner) — explicit override
    // first, then auto-detect from the server's own container, fallback to bridge.
    // Without this the sandbox lands on Docker's default bridge with no DNS for
    // `redis` / `host.docker.internal`, and the sandbox-agent's Redis connect
    // times out with a confusing UnableToConnect.
    private async Task<string> ResolveNetworkAsync(CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger<DockerSandboxFactory>();
        if (!string.IsNullOrWhiteSpace(options.Network))
        {
            logger.LogDebug("Using configured sandbox network: {Network}", options.Network);
            return options.Network;
        }
        try
        {
            var hostname = System.Net.Dns.GetHostName();
            var info = await docker.Containers.InspectContainerAsync(hostname, ct);
            var network = info.NetworkSettings?.Networks?.Keys.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(network))
            {
                logger.LogDebug("Auto-detected sandbox network: {Network}", network);
                return network;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug("Could not auto-detect sandbox network: {Message}", ex.Message);
        }
        logger.LogDebug("Falling back to bridge network for sandbox");
        return "bridge";
    }

    private async Task CreateVolumesAsync(string shared, string work, CancellationToken ct)
    {
        await docker.Volumes.CreateAsync(new VolumesCreateParameters { Name = shared }, ct);
        await docker.Volumes.CreateAsync(new VolumesCreateParameters { Name = work }, ct);
    }

    private async Task RunLoaderAsync(string slug, string sharedVolume, string agentImage, string network, CancellationToken ct)
    {
        await EnsureImagePresentAsync(agentImage, isCarrier: true, ct);
        var spec = specBuilder.BuildLoader($"agentsmith-sandbox-loader-{slug}", sharedVolume, agentImage);
        if (spec.HostConfig is not null) spec.HostConfig.NetworkMode = network;
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
        SandboxSpec spec, string network, CancellationToken ct)
    {
        await EnsureImagePresentAsync(spec.ToolchainImage, isCarrier: false, ct);
        var containerSpec = specBuilder.BuildToolchain(
            $"agentsmith-sandbox-{slug}", sharedVolume, workVolume, jobId, options.RedisUrl, spec);
        if (containerSpec.HostConfig is not null) containerSpec.HostConfig.NetworkMode = network;
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
