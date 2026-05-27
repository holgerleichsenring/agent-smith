using AgentSmith.Contracts.Constants;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentSmith.Server.Services;

/// <summary>
/// Spawns a Kubernetes Job for each agent request.
/// Each job runs an ephemeral agent-smith container with the command details
/// and Redis bus connection injected via environment variables.
/// Selected when SPAWNER_TYPE=kubernetes (or when unset, for prod compatibility).
/// </summary>
public sealed class KubernetesJobSpawner(
    IKubernetes k8sClient,
    IOptions<JobSpawnerOptions> options,
    ILogger<KubernetesJobSpawner> logger) : IJobSpawner
{
    private readonly JobSpawnerOptions _options = options.Value;
    private readonly string _redisUrl =
        Environment.GetEnvironmentVariable(AgentEnvKeys.RedisUrl) ?? "redis:6379";

    public async Task<string> SpawnAsync(
        JobRequest request,
        CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid().ToString("N")[..12];
        var jobName = $"agentsmith-{jobId}";

        var job = BuildJob(jobName, jobId, request, _redisUrl);

        await k8sClient.BatchV1.CreateNamespacedJobAsync(
            job, _options.Namespace, cancellationToken: cancellationToken);

        logger.LogInformation(
            "Spawned K8s Job {JobName} (id={JobId}) for {Command} in {Project}",
            jobName, jobId, request.InputCommand, request.Project);

        return jobId;
    }

    public async Task<bool> IsAliveAsync(string jobId, CancellationToken cancellationToken)
    {
        var jobName = $"agentsmith-{jobId}";

        try
        {
            var job = await k8sClient.BatchV1.ReadNamespacedJobAsync(
                jobName, _options.Namespace, cancellationToken: cancellationToken);

            // Active > 0 means the pod is still running
            return job.Status?.Active is > 0;
        }
        catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Job already cleaned up by TTL controller
            return false;
        }
    }

    private V1Job BuildJob(string jobName, string jobId, JobRequest request, string redisUrl) => new()
    {
        Metadata = new V1ObjectMeta
        {
            Name = jobName,
            NamespaceProperty = _options.Namespace,
            Labels = new Dictionary<string, string>
            {
                ["app"] = "agentsmith",
                ["job-id"] = jobId,
                ["platform"] = request.Platform,
                ["project"] = request.Project
            }
        },
        Spec = new V1JobSpec
        {
            TtlSecondsAfterFinished = _options.TtlSecondsAfterFinished,
            BackoffLimit = 0,
            Template = new V1PodTemplateSpec
            {
                Metadata = new V1ObjectMeta
                {
                    Labels = new Dictionary<string, string>
                    {
                        ["app"] = "agentsmith",
                        ["job-id"] = jobId
                    }
                },
                Spec = new V1PodSpec
                {
                    RestartPolicy = "Never",
                    Containers =
                    [
                        new V1Container
                        {
                            Name = "agentsmith",
                            Image = request.OrchestratorImage,
                            ImagePullPolicy = _options.ImagePullPolicy,
                            Args = BuildArgs(jobId, request, redisUrl),
                            Env = BuildEnv(jobId, request),
                            Resources = BuildResources(request.OrchestratorResources)
                        }
                    ]
                }
            }
        }
    };

    private static List<string> BuildArgs(string jobId, JobRequest request, string redisUrl)
    {
        var args = new List<string>
        {
            "run",
            "--headless",
            "--job-id", jobId,
            "--redis-url", redisUrl,
            "--platform", request.Platform,
            "--channel-id", request.ChannelId,
        };

        if (!string.IsNullOrEmpty(request.PipelineOverride))
            args.AddRange(["--pipeline", request.PipelineOverride]);

        args.Add(request.InputCommand);
        return args;
    }

    private List<V1EnvVar> BuildEnv(string jobId, JobRequest request)
    {
        var env = AgentSecretBinding.All
            .Select(b => EnvFromSecret(b.EnvVar, _options.SecretName, b.K8sSecretKey))
            .ToList();
        env.AddRange(
        [
            new V1EnvVar { Name = "JOB_ID", Value = jobId },
            new V1EnvVar { Name = "PROJECT", Value = request.Project },
            new V1EnvVar { Name = "CHANNEL_ID", Value = request.ChannelId },
            new V1EnvVar { Name = "USER_ID", Value = request.UserId },
            new V1EnvVar { Name = "AGENTSMITH_PLATFORM", Value = request.Platform }
        ]);
        return env;
    }

    private static V1ResourceRequirements BuildResources(ResourceLimits resources) => new()
    {
        Requests = new Dictionary<string, ResourceQuantity>
        {
            ["cpu"] = new(resources.CpuRequest),
            ["memory"] = new(resources.MemoryRequest)
        },
        Limits = new Dictionary<string, ResourceQuantity>
        {
            ["cpu"] = new(resources.CpuLimit),
            ["memory"] = new(resources.MemoryLimit)
        }
    };

    private static V1EnvVar EnvFromSecret(string envName, string secretName, string secretKey) =>
        new()
        {
            Name = envName,
            ValueFrom = new V1EnvVarSource
            {
                SecretKeyRef = new V1SecretKeySelector
                {
                    Name = secretName,
                    Key = secretKey,
                    Optional = true
                }
            }
        };
}
