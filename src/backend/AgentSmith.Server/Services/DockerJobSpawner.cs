using System.Diagnostics;
using AgentSmith.Contracts.Constants;
using AgentSmith.Contracts.Providers;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Sandbox;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentSmith.Server.Services;

/// <summary>
/// Spawns ephemeral agent containers via the Docker Engine API.
/// Used in local Docker Compose mode (SPAWNER_TYPE=docker).
/// </summary>
public sealed class DockerJobSpawner(
    IOptions<JobSpawnerOptions> options,
    ILogger<DockerJobSpawner> logger) : IJobSpawner
{
    private readonly JobSpawnerOptions _options = options.Value;
    private static readonly string[] ForwardedEnvVars =
    [
        AgentEnvKeys.AnthropicApiKey, AgentEnvKeys.OpenAiApiKey, AgentEnvKeys.GeminiApiKey,
        AgentEnvKeys.GitHubToken, AgentEnvKeys.AzureDevOpsToken, AgentEnvKeys.GitLabToken,
        AgentEnvKeys.JiraToken, AgentEnvKeys.JiraEmail, AgentEnvKeys.RedisUrl,
    ];

    public async Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var client = CreateDockerClient();
            await client.System.PingAsync(cancellationToken);
            return ConnectionProbeResult.Reachable(stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Docker daemon probe failed");
            return ConnectionProbeResult.Unreachable(stopwatch.ElapsedMilliseconds, ex.Message);
        }
    }

    public async Task<string> SpawnAsync(JobRequest request, CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid().ToString("N")[..12];
        var containerName = $"agentsmith-{jobId}";
        using var client = CreateDockerClient();

        var networkResolver = new DockerNetworkResolver(options, logger);
        var network = await networkResolver.ResolveAsync(client, cancellationToken);

        var createParams = new CreateContainerParameters
        {
            Name = containerName, Image = request.OrchestratorImage,
            Cmd = BuildArgs(jobId, request), Env = BuildEnv(jobId, request),
            Labels = new Dictionary<string, string>
            {
                ["app"] = "agentsmith", ["job-id"] = jobId,
                ["platform"] = request.Platform, ["project"] = request.Project,
                ["managed-by"] = "agentsmith-dispatcher"
            },
            HostConfig = new HostConfig
            {
                AutoRemove = true, NetworkMode = network,
                Memory = request.OrchestratorResources.MemoryLimitToBytes(),
                MemoryReservation = request.OrchestratorResources.MemoryRequestToBytes(),
                NanoCPUs = request.OrchestratorResources.CpuLimitToNanoCpus(),
            }
        };

        logger.LogInformation("Creating Docker container {Name} (id={JobId}) for {Command} in {Project}",
            containerName, jobId, request.InputCommand, request.Project);

        CreateContainerResponse response;
        try { response = await client.Containers.CreateContainerAsync(createParams, cancellationToken); }
        catch (DockerImageNotFoundException)
        {
            throw new InvalidOperationException(
                $"Orchestrator image '{request.OrchestratorImage}' not found. Pull it or run: docker build -t {request.OrchestratorImage} .");
        }

        await client.Containers.StartContainerAsync(response.ID, new ContainerStartParameters(), cancellationToken);
        logger.LogInformation("Started container {Name} (jobId={JobId})", containerName, jobId);
        return jobId;
    }

    public async Task<bool> IsAliveAsync(string jobId, CancellationToken cancellationToken)
    {
        using var client = CreateDockerClient();
        try
        {
            var inspection = await client.Containers.InspectContainerAsync(
                $"agentsmith-{jobId}", cancellationToken);
            return inspection.State.Running;
        }
        catch (DockerContainerNotFoundException) { return false; }
    }

    // p0330: the cancel enforcer's force-kill. Force-remove stops + removes the
    // named orchestrator container (same handle IsAliveAsync inspects); its
    // sandboxes run in-process, so the container is the whole run footprint.
    // Idempotent: an already-removed container is a no-op.
    public async Task TerminateAsync(string jobId, CancellationToken cancellationToken)
    {
        using var client = CreateDockerClient();
        try
        {
            await client.Containers.RemoveContainerAsync(
                $"agentsmith-{jobId}",
                new ContainerRemoveParameters { Force = true }, cancellationToken);
            logger.LogInformation("Removed container agentsmith-{JobId} (cancel enforcement)", jobId);
        }
        catch (DockerContainerNotFoundException)
        {
            logger.LogDebug("Container agentsmith-{JobId} already gone — terminate is a no-op", jobId);
        }
    }

    private static DockerClient CreateDockerClient()
    {
        var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");
        var uri = dockerHost is not null ? new Uri(dockerHost)
            : OperatingSystem.IsWindows() ? new Uri("npipe://./pipe/docker_engine")
            : new Uri("unix:///var/run/docker.sock");
        return new DockerClientConfiguration(uri).CreateClient();
    }

    private static List<string> BuildArgs(string jobId, JobRequest request)
    {
        var args = new List<string>
        {
            "run", "--headless", "--job-id", jobId,
            "--redis-url", Environment.GetEnvironmentVariable(AgentEnvKeys.RedisUrl) ?? "redis:6379",
            "--platform", request.Platform, "--channel-id", request.ChannelId,
        };
        if (!string.IsNullOrEmpty(request.PipelineOverride))
            args.AddRange(["--pipeline", request.PipelineOverride]);
        args.Add(request.InputCommand);
        return args;
    }

    private static List<string> BuildEnv(string jobId, JobRequest request)
    {
        var env = new List<string>
        {
            $"JOB_ID={jobId}", $"PROJECT={request.Project}",
            $"CHANNEL_ID={request.ChannelId}", $"USER_ID={request.UserId}",
            $"AGENTSMITH_PLATFORM={request.Platform}",
        };
        foreach (var varName in ForwardedEnvVars)
        {
            var value = Environment.GetEnvironmentVariable(varName);
            if (!string.IsNullOrEmpty(value)) env.Add($"{varName}={value}");
        }
        return env;
    }
}
