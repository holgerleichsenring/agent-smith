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
        var spec = specBuilder.BuildLoader($"agentsmith-sandbox-loader-{slug}", sharedVolume, agentImage);
        var created = await docker.Containers.CreateContainerAsync(spec, ct);
        await docker.Containers.StartContainerAsync(created.ID, new ContainerStartParameters(), ct);
        await docker.Containers.WaitContainerAsync(created.ID, ct);
        await docker.Containers.RemoveContainerAsync(created.ID,
            new ContainerRemoveParameters { Force = true }, ct);
    }

    private async Task<string> StartToolchainAsync(
        string slug, string sharedVolume, string workVolume, string jobId,
        SandboxSpec spec, CancellationToken ct)
    {
        var containerSpec = specBuilder.BuildToolchain(
            $"agentsmith-sandbox-{slug}", sharedVolume, workVolume, jobId, options.RedisUrl, spec);
        var created = await docker.Containers.CreateContainerAsync(containerSpec, ct);
        await docker.Containers.StartContainerAsync(created.ID, new ContainerStartParameters(), ct);
        loggerFactory.CreateLogger<DockerSandboxFactory>()
            .LogInformation("Sandbox container {Id} started for job {JobId}", created.ID, jobId);
        return created.ID;
    }
}
