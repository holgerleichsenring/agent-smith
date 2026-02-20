using AgentSmith.Dispatcher.Models;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Dispatcher.Services;

/// <summary>
/// Spawns a Kubernetes Job for each FixTicketIntent.
/// Each job runs an ephemeral agent-smith container with the ticket details
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
        FixTicketIntent intent,
        CancellationToken cancellationToken = default)
    {
        var jobId = Guid.NewGuid().ToString("N")[..12];
        var jobName = $"agentsmith-{jobId}";

        var job = BuildJob(jobName, jobId, intent, _redisUrl);

        await k8sClient.BatchV1.CreateNamespacedJobAsync(
            job, options.Namespace, cancellationToken: cancellationToken);

        logger.LogInformation(
            "Spawned K8s Job {JobName} (id={JobId}) for ticket #{TicketId} in {Project}",
            jobName, jobId, intent.TicketId, intent.Project);

        return jobId;
    }

    private V1Job BuildJob(string jobName, string jobId, FixTicketIntent intent, string redisUrl) => new()
    {
        Metadata = new V1ObjectMeta
        {
            Name = jobName,
            NamespaceProperty = options.Namespace,
            Labels = new Dictionary<string, string>
            {
                ["app"] = "agentsmith",
                ["job-id"] = jobId,
                ["platform"] = intent.Platform,
                ["project"] = intent.Project
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
                            Args = BuildArgs(jobId, intent, redisUrl),
                            Env = BuildEnv(jobId, intent),
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

    private static List<string> BuildArgs(string jobId, FixTicketIntent intent, string redisUrl) =>
    [
        "--headless",
        "--job-id", jobId,
        "--redis-url", redisUrl,
        "--platform", intent.Platform,
        "--channel-id", intent.ChannelId,
        $"fix #{intent.TicketId} in {intent.Project}"
    ];

    private List<V1EnvVar> BuildEnv(string jobId, FixTicketIntent intent) =>
    [
        EnvFromSecret("ANTHROPIC_API_KEY", options.SecretName, "anthropic-api-key"),
        EnvFromSecret("AZURE_DEVOPS_TOKEN", options.SecretName, "azure-devops-token"),
        EnvFromSecret("GITHUB_TOKEN", options.SecretName, "github-token"),
        EnvFromSecret("OPENAI_API_KEY", options.SecretName, "openai-api-key"),
        EnvFromSecret("GEMINI_API_KEY", options.SecretName, "gemini-api-key"),
        EnvFromSecret("REDIS_URL", options.SecretName, "redis-url"),
        new V1EnvVar { Name = "JOB_ID", Value = jobId },
        new V1EnvVar { Name = "TICKET_ID", Value = intent.TicketId.ToString() },
        new V1EnvVar { Name = "PROJECT", Value = intent.Project },
        new V1EnvVar { Name = "CHANNEL_ID", Value = intent.ChannelId },
        new V1EnvVar { Name = "USER_ID", Value = intent.UserId },
        new V1EnvVar { Name = "PLATFORM", Value = intent.Platform }
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
    public string Image { get; set; } = "agentsmith:latest";

    /// <summary>Image pull policy. Use IfNotPresent locally, Always in prod.</summary>
    public string ImagePullPolicy { get; set; } = "IfNotPresent";

    /// <summary>K8s Secret name containing API tokens. Only used by KubernetesJobSpawner.</summary>
    public string SecretName { get; set; } = "agentsmith-secrets";

    /// <summary>Seconds after job completion before K8s cleans it up. Only used by KubernetesJobSpawner.</summary>
    public int TtlSecondsAfterFinished { get; set; } = 300;

    /// <summary>Docker network to attach spawned containers to. Only used by DockerJobSpawner.</summary>
    public string DockerNetwork { get; set; } = string.Empty;
}
