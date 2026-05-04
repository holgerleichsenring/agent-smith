using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services;

/// <summary>
/// Spawns ephemeral agent containers via the Docker Engine API.
/// Used in local Docker Compose mode (SPAWNER_TYPE=docker).
/// </summary>
public sealed class DockerJobSpawner(
    JobSpawnerOptions options,
    ILogger<DockerJobSpawner> logger) : IJobSpawner
{
    private static readonly string[] ForwardedEnvVars =
    [
        "ANTHROPIC_API_KEY", "OPENAI_API_KEY", "GEMINI_API_KEY",
        "GITHUB_TOKEN", "AZURE_DEVOPS_TOKEN", "GITLAB_TOKEN",
        "JIRA_TOKEN", "JIRA_EMAIL", "REDIS_URL",
    ];

    public async Task<string> SpawnAsync(JobRequest request, CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid().ToString("N")[..12];
        var containerName = $"agentsmith-{jobId}";
        using var client = CreateDockerClient();

        var networkResolver = new DockerNetworkResolver(options, logger);
        var network = await networkResolver.ResolveAsync(client, cancellationToken);

        var createParams = new CreateContainerParameters
        {
            Name = containerName, Image = options.Image,
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
                Memory = 1 * 1024 * 1024 * 1024L, MemoryReservation = 512 * 1024 * 1024L,
                NanoCPUs = 1_000_000_000L,
            }
        };

        logger.LogInformation("Creating Docker container {Name} (id={JobId}) for {Command} in {Project}",
            containerName, jobId, request.InputCommand, request.Project);

        CreateContainerResponse response;
        try { response = await client.Containers.CreateContainerAsync(createParams, cancellationToken); }
        catch (DockerImageNotFoundException)
        {
            throw new InvalidOperationException(
                $"Agent image '{options.Image}' not found. Run: docker build -t {options.Image} .");
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
            "--redis-url", Environment.GetEnvironmentVariable("REDIS_URL") ?? "redis:6379",
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
