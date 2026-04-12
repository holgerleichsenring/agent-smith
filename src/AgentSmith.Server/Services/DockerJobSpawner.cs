using AgentSmith.Dispatcher.Contracts;
using AgentSmith.Dispatcher.Models;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Dispatcher.Services;

/// <summary>
/// Spawns ephemeral agent containers via the Docker Engine API (Docker socket).
/// Used in local Docker Compose mode (SPAWNER_TYPE=docker).
///
/// The spawned container joins the same Docker network as the Dispatcher,
/// so it can reach Redis at redis:6379 without any host.docker.internal hacks.
/// AutoRemove=true replaces the K8s TTL — the container cleans itself up.
///
/// Environment variables are forwarded directly from the Dispatcher's own
/// environment — no K8s Secret required in local dev.
/// </summary>
public sealed class DockerJobSpawner(
    JobSpawnerOptions options,
    ILogger<DockerJobSpawner> logger) : IJobSpawner
{
    // Environment variables forwarded from the Dispatcher to the agent container.
    private static readonly string[] ForwardedEnvVars =
    [
        "ANTHROPIC_API_KEY",
        "OPENAI_API_KEY",
        "GEMINI_API_KEY",
        "GITHUB_TOKEN",
        "AZURE_DEVOPS_TOKEN",
        "GITLAB_TOKEN",
        "JIRA_TOKEN",
        "JIRA_EMAIL",
        "REDIS_URL",
    ];

    public async Task<string> SpawnAsync(
        JobRequest request,
        CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid().ToString("N")[..12];
        var containerName = $"agentsmith-{jobId}";

        using var client = CreateDockerClient();

        var env = BuildEnv(jobId, request);
        var args = BuildArgs(jobId, request);
        var network = await ResolveNetworkAsync(client, cancellationToken);

        var createParams = new CreateContainerParameters
        {
            Name = containerName,
            Image = options.Image,
            Cmd = args,
            Env = env,
            Labels = new Dictionary<string, string>
            {
                ["app"] = "agentsmith",
                ["job-id"] = jobId,
                ["platform"] = request.Platform,
                ["project"] = request.Project,
                ["managed-by"] = "agentsmith-dispatcher"
            },
            HostConfig = new HostConfig
            {
                AutoRemove = true,
                NetworkMode = network,
                // Same resource limits as the K8s job
                Memory = 1 * 1024 * 1024 * 1024L,         // 1 GiB
                MemoryReservation = 512 * 1024 * 1024L,    // 512 MiB
                NanoCPUs = 1_000_000_000L,                  // 1 CPU
            }
        };

        logger.LogInformation(
            "Creating Docker container {ContainerName} (id={JobId}) for {Command} in {Project} on network {Network}",
            containerName, jobId, request.InputCommand, request.Project, network);

        CreateContainerResponse response;
        try
        {
            response = await client.Containers.CreateContainerAsync(
                createParams, cancellationToken);
        }
        catch (DockerImageNotFoundException)
        {
            throw new InvalidOperationException(
                $"Agent image '{options.Image}' not found. Run: docker build -t {options.Image} .");
        }

        await client.Containers.StartContainerAsync(
            response.ID, new ContainerStartParameters(), cancellationToken);

        logger.LogInformation(
            "Started Docker container {ContainerName} (containerId={ContainerId}, jobId={JobId})",
            containerName, response.ID[..12], jobId);

        return jobId;
    }

    // --- Private helpers ---

    public async Task<bool> IsAliveAsync(string jobId, CancellationToken cancellationToken)
    {
        var containerName = $"agentsmith-{jobId}";
        using var client = CreateDockerClient();

        try
        {
            var inspection = await client.Containers.InspectContainerAsync(
                containerName, cancellationToken);

            // Running means the container is actively executing
            return inspection.State.Running;
        }
        catch (DockerContainerNotFoundException)
        {
            // AutoRemove=true means dead containers are already gone
            return false;
        }
    }

    private static DockerClient CreateDockerClient()
    {
        // Prefer explicit DOCKER_HOST env var, fall back to platform default socket
        var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");

        var uri = dockerHost is not null
            ? new Uri(dockerHost)
            : OperatingSystem.IsWindows()
                ? new Uri("npipe://./pipe/docker_engine")
                : new Uri("unix:///var/run/docker.sock");

        return new DockerClientConfiguration(uri).CreateClient();
    }

    /// <summary>
    /// Resolves the Docker network to attach the agent container to.
    ///
    /// Priority:
    /// 1. Explicit DockerNetwork option (from DOCKER_NETWORK env var)
    /// 2. Auto-detect: find the network the Dispatcher container itself is attached to
    /// 3. Fallback: "bridge" (Docker default)
    /// </summary>
    private async Task<string> ResolveNetworkAsync(
        DockerClient client,
        CancellationToken cancellationToken)
    {
        // 1. Explicit override
        if (!string.IsNullOrWhiteSpace(options.DockerNetwork))
        {
            logger.LogDebug("Using configured Docker network: {Network}", options.DockerNetwork);
            return options.DockerNetwork;
        }

        // 2. Auto-detect from hostname (container ID in Docker)
        try
        {
            var hostname = System.Net.Dns.GetHostName();
            var containerInfo = await client.Containers.InspectContainerAsync(
                hostname, cancellationToken);

            var network = containerInfo.NetworkSettings?.Networks?.Keys.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(network))
            {
                logger.LogDebug("Auto-detected Docker network: {Network}", network);
                return network;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                "Could not auto-detect Docker network (running outside container?): {Message}",
                ex.Message);
        }

        // 3. Fallback
        logger.LogDebug("Falling back to Docker network: bridge");
        return "bridge";
    }

    private static List<string> BuildArgs(string jobId, JobRequest request)
    {
        var args = new List<string>
        {
            "run",
            "--headless",
            "--job-id", jobId,
            "--redis-url", Environment.GetEnvironmentVariable("REDIS_URL") ?? "redis:6379",
            "--platform", request.Platform,
            "--channel-id", request.ChannelId,
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
            $"JOB_ID={jobId}",
            $"PROJECT={request.Project}",
            $"CHANNEL_ID={request.ChannelId}",
            $"USER_ID={request.UserId}",
            $"AGENTSMITH_PLATFORM={request.Platform}",
        };

        // Forward secrets from Dispatcher's own environment — no K8s Secret needed locally
        foreach (var varName in ForwardedEnvVars)
        {
            var value = Environment.GetEnvironmentVariable(varName);
            if (!string.IsNullOrEmpty(value))
                env.Add($"{varName}={value}");
        }

        return env;
    }
}
