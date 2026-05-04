using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services;

/// <summary>
/// Spawns a Kubernetes Job for each agent request.
/// Each job runs an ephemeral agent-smith container with the command details
/// and Redis bus connection injected via environment variables.
/// Selected when SPAWNER_TYPE=kubernetes (or when unset, for prod compatibility).
/// </summary>
public sealed class KubernetesJobSpawner(
    IKubernetes k8sClient,
    JobSpawnerOptions options,
    ILogger<KubernetesJobSpawner> logger) : IJobSpawner
{
    private readonly string _redisUrl =
        Environment.GetEnvironmentVariable("REDIS_URL") ?? "redis:6379";

    public async Task<string> SpawnAsync(
        JobRequest request,
        CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid().ToString("N")[..12];
        var jobName = $"agentsmith-{jobId}";

        var job = BuildJob(jobName, jobId, request, _redisUrl);

        await k8sClient.BatchV1.CreateNamespacedJobAsync(
            job, options.Namespace, cancellationToken: cancellationToken);

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
                jobName, options.Namespace, cancellationToken: cancellationToken);

            // Active > 0 means the pod is still running
            return job.Status?.Active is > 0;
        }
        catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Job already cleaned up by TTL controller
            return false;
        }
    }

    public async Task SpawnQueueJobAsync(
        string jobId, string redisUrl, string configPath, CancellationToken cancellationToken)
    {
        var jobName = $"agentsmith-{jobId}";
        var job = BuildQueueJob(jobName, jobId, redisUrl, configPath);

        await k8sClient.BatchV1.CreateNamespacedJobAsync(
            job, options.Namespace, cancellationToken: cancellationToken);

        logger.LogInformation(
            "Spawned K8s queue Job {JobName} (id={JobId})", jobName, jobId);
    }

    private V1Job BuildQueueJob(string jobName, string jobId, string redisUrl, string configPath) => new()
    {
        Metadata = new V1ObjectMeta
        {
            Name = jobName,
            NamespaceProperty = options.Namespace,
            Labels = new Dictionary<string, string>
            {
                ["app"] = "agentsmith",
                ["job-id"] = jobId,
                ["spawned-from"] = "queue"
            }
        },
        Spec = new V1JobSpec
        {
            TtlSecondsAfterFinished = options.TtlSecondsAfterFinished,
            BackoffLimit = 0,
            Template = new V1PodTemplateSpec
            {
                Metadata = new V1ObjectMeta
                {
                    Labels = new Dictionary<string, string>
                    {
                        ["app"] = "agentsmith",
                        ["job-id"] = jobId,
                        ["spawned-from"] = "queue"
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
                            Image = options.Image,
                            ImagePullPolicy = options.ImagePullPolicy,
                            Args =
                            [
                                "run-claimed-job",
                                "--job-id", jobId,
                                "--redis-url", redisUrl,
                                "--config", configPath,
                            ],
                            Env = BuildQueueEnv(jobId, redisUrl),
                            Resources = new V1ResourceRequirements
                            {
                                Requests = new Dictionary<string, ResourceQuantity>
                                {
                                    ["cpu"] = new("250m"),
                                    ["memory"] = new("512Mi")
                                },
                                Limits = new Dictionary<string, ResourceQuantity>
                                {
                                    ["cpu"] = new("1000m"),
                                    ["memory"] = new("1Gi")
                                }
                            }
                        }
                    ]
                }
            }
        }
    };

    private List<V1EnvVar> BuildQueueEnv(string jobId, string redisUrl) =>
    [
        EnvFromSecret("ANTHROPIC_API_KEY", options.SecretName, "anthropic-api-key"),
        EnvFromSecret("AZURE_DEVOPS_TOKEN", options.SecretName, "azure-devops-token"),
        EnvFromSecret("GITHUB_TOKEN", options.SecretName, "github-token"),
        EnvFromSecret("OPENAI_API_KEY", options.SecretName, "openai-api-key"),
        EnvFromSecret("AZURE_OPENAI_API_KEY", options.SecretName, "azure-openai-api-key"),
        EnvFromSecret("GEMINI_API_KEY", options.SecretName, "gemini-api-key"),
        EnvFromSecret("GITLAB_TOKEN", options.SecretName, "gitlab-token"),
        EnvFromSecret("JIRA_TOKEN", options.SecretName, "jira-token"),
        EnvFromSecret("JIRA_EMAIL", options.SecretName, "jira-email"),
        new V1EnvVar { Name = "REDIS_URL", Value = redisUrl },
        new V1EnvVar { Name = "JOB_ID", Value = jobId },
    ];

    private V1Job BuildJob(string jobName, string jobId, JobRequest request, string redisUrl) => new()
    {
        Metadata = new V1ObjectMeta
        {
            Name = jobName,
            NamespaceProperty = options.Namespace,
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
            TtlSecondsAfterFinished = options.TtlSecondsAfterFinished,
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
                            Image = options.Image,
                            ImagePullPolicy = options.ImagePullPolicy,
                            Args = BuildArgs(jobId, request, redisUrl),
                            Env = BuildEnv(jobId, request),
                            Resources = new V1ResourceRequirements
                            {
                                Requests = new Dictionary<string, ResourceQuantity>
                                {
                                    ["cpu"] = new("250m"),
                                    ["memory"] = new("512Mi")
                                },
                                Limits = new Dictionary<string, ResourceQuantity>
                                {
                                    ["cpu"] = new("1000m"),
                                    ["memory"] = new("1Gi")
                                }
                            }
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

    private List<V1EnvVar> BuildEnv(string jobId, JobRequest request) =>
    [
        EnvFromSecret("ANTHROPIC_API_KEY", options.SecretName, "anthropic-api-key"),
        EnvFromSecret("AZURE_DEVOPS_TOKEN", options.SecretName, "azure-devops-token"),
        EnvFromSecret("GITHUB_TOKEN", options.SecretName, "github-token"),
        EnvFromSecret("OPENAI_API_KEY", options.SecretName, "openai-api-key"),
        EnvFromSecret("GEMINI_API_KEY", options.SecretName, "gemini-api-key"),
        EnvFromSecret("REDIS_URL", options.SecretName, "redis-url"),
        new V1EnvVar { Name = "JOB_ID", Value = jobId },
        new V1EnvVar { Name = "PROJECT", Value = request.Project },
        new V1EnvVar { Name = "CHANNEL_ID", Value = request.ChannelId },
        new V1EnvVar { Name = "USER_ID", Value = request.UserId },
        new V1EnvVar { Name = "AGENTSMITH_PLATFORM", Value = request.Platform }
    ];

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

/// <summary>
/// Configuration options for job spawning, shared by both KubernetesJobSpawner
/// and DockerJobSpawner where applicable.
/// </summary>
public sealed class JobSpawnerOptions
{
    /// <summary>Kubernetes namespace for spawned jobs. Only used by KubernetesJobSpawner.</summary>
    public string Namespace { get; set; } = "default";

    /// <summary>Agent container image name.</summary>
    public string Image { get; set; } = "agentsmith-cli:latest";

    /// <summary>Image pull policy. Use IfNotPresent locally, Always in prod.</summary>
    public string ImagePullPolicy { get; set; } = "IfNotPresent";

    /// <summary>K8s Secret name containing API tokens. Only used by KubernetesJobSpawner.</summary>
    public string SecretName { get; set; } = "agentsmith-secrets";

    /// <summary>Seconds after job completion before K8s cleans it up. Only used by KubernetesJobSpawner.</summary>
    public int TtlSecondsAfterFinished { get; set; } = 300;

    /// <summary>Docker network to attach spawned containers to. Only used by DockerJobSpawner.</summary>
    public string DockerNetwork { get; set; } = string.Empty;
}
