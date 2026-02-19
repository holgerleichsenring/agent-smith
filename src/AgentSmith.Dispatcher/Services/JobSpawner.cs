using AgentSmith.Dispatcher.Models;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Dispatcher.Services;

/// <summary>
/// Spawns a Kubernetes Job for each FixTicketIntent.
/// Each job runs an ephemeral agent-smith container with the ticket details
/// and Redis bus connection injected via environment variables.
/// </summary>
public sealed class JobSpawner(
    IKubernetes k8sClient,
    JobSpawnerOptions options,
    ILogger<JobSpawner> logger)
{
    public async Task<string> SpawnAsync(
        FixTicketIntent intent,
        CancellationToken cancellationToken = default)
    {
        var jobId = Guid.NewGuid().ToString("N")[..12];
        var jobName = $"agentsmith-{jobId}";

        var job = BuildJob(jobName, jobId, intent);

        await k8sClient.BatchV1.CreateNamespacedJobAsync(
            job, options.Namespace, cancellationToken: cancellationToken);

        logger.LogInformation(
            "Spawned K8s Job {JobName} (id={JobId}) for ticket #{TicketId} in {Project}",
            jobName, jobId, intent.TicketId, intent.Project);

        return jobId;
    }

    private V1Job BuildJob(string jobName, string jobId, FixTicketIntent intent) => new()
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
                            Args = BuildArgs(jobId, intent),
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

    private static List<string> BuildArgs(string jobId, FixTicketIntent intent) =>
    [
        "--headless",
        "--job-id", jobId,
        "--redis-url", "$(REDIS_URL)",
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
/// Configuration options for JobSpawner, bound from appsettings / environment.
/// </summary>
public sealed class JobSpawnerOptions
{
    public string Namespace { get; set; } = "default";
    public string Image { get; set; } = "agentsmith:latest";
    public string ImagePullPolicy { get; set; } = "IfNotPresent";
    public string SecretName { get; set; } = "agentsmith-secrets";
    public int TtlSecondsAfterFinished { get; set; } = 300;
}
