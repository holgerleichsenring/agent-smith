using AgentSmith.Contracts.Sandbox;
using k8s.Models;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// Builds the V1Pod spec for the sandbox: agent-loader initContainer + toolchain
/// main container, sharing /shared (binary) and /work (working dir) emptyDir volumes.
/// Pod-level fsGroup makes /shared/agent group-readable+executable from non-root toolchains.
/// </summary>
public sealed class PodSpecBuilder
{
    private const string SharedVolume = "agent-shared";
    private const string WorkVolume = "agent-work";
    private const string SharedMount = "/shared";
    private const string WorkMount = "/work";

    public V1Pod Build(string podName, string jobId, string redisUrl, SandboxSpec spec, V1OwnerReference? owner)
    {
        var security = spec.SecurityContext ?? new SandboxSecurityContext();
        return new V1Pod
        {
            Metadata = BuildMetadata(podName, jobId, owner),
            Spec = new V1PodSpec
            {
                RestartPolicy = "Never",
                SecurityContext = new V1PodSecurityContext { FsGroup = security.FsGroup },
                InitContainers = [BuildInitContainer(spec.AgentImage)],
                Containers = [BuildToolchainContainer(spec, jobId, redisUrl)],
                Volumes = BuildVolumes()
            }
        };
    }

    private static V1ObjectMeta BuildMetadata(string podName, string jobId, V1OwnerReference? owner) => new()
    {
        Name = podName,
        Labels = new Dictionary<string, string>
        {
            ["app"] = "agentsmith-sandbox",
            ["pipeline-id"] = jobId
        },
        OwnerReferences = owner is null ? null : [owner]
    };

    private static V1Container BuildInitContainer(string agentImage) => new()
    {
        Name = "agent-loader",
        Image = agentImage,
        Args = ["--inject", $"{SharedMount}/agent"],
        VolumeMounts = [new V1VolumeMount { Name = SharedVolume, MountPath = SharedMount }],
        // Fixed limits — loader only extracts the bundled .NET single-file agent
        // (~75 MB) and copies one binary, then exits. Namespaces with a
        // ResourceQuota that mandates limits.cpu/limits.memory on every
        // container (init + main) reject the pod otherwise; pod creation fails
        // with "must specify limits.cpu for: agent-loader" before the workload
        // even starts. Values are sized for the bundle extraction headroom plus
        // a brief CPU burst on startup.
        Resources = new V1ResourceRequirements
        {
            Limits = new Dictionary<string, ResourceQuantity>
            {
                ["cpu"] = new("200m"),
                ["memory"] = new("256Mi")
            },
            Requests = new Dictionary<string, ResourceQuantity>
            {
                ["cpu"] = new("50m"),
                ["memory"] = new("128Mi")
            }
        }
    };

    private static V1Container BuildToolchainContainer(SandboxSpec spec, string jobId, string redisUrl) => new()
    {
        Name = "toolchain",
        Image = spec.ToolchainImage,
        Command = [$"{SharedMount}/agent"],
        Args = ["--redis-url", redisUrl, "--job-id", jobId],
        Env = BuildEnv(jobId, redisUrl, spec.GitTokenSecretRef),
        VolumeMounts =
        [
            new V1VolumeMount { Name = SharedVolume, MountPath = SharedMount, ReadOnlyProperty = true },
            new V1VolumeMount { Name = WorkVolume, MountPath = WorkMount }
        ],
        WorkingDir = WorkMount,
        Resources = BuildToolchainResources(spec.Resources)
    };

    // Both Requests and Limits are emitted: Requests so the pod survives namespaces
    // with a ResourceQuota that mandates limits.cpu + requests.cpu + memory variants,
    // Limits so the toolchain container has a hard CFS/OOM cap. Quantities are passed
    // through to Kubernetes verbatim from <see cref="ResourceLimits"/>.
    private static V1ResourceRequirements BuildToolchainResources(ResourceLimits resources) => new()
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

    private static List<V1EnvVar> BuildEnv(string jobId, string redisUrl, SecretRef? gitToken)
    {
        var env = new List<V1EnvVar>
        {
            new() { Name = "JOB_ID", Value = jobId },
            new() { Name = "REDIS_URL", Value = redisUrl }
        };
        if (gitToken is not null)
        {
            env.Add(new V1EnvVar
            {
                Name = "GIT_TOKEN",
                ValueFrom = new V1EnvVarSource
                {
                    SecretKeyRef = new V1SecretKeySelector
                    {
                        Name = gitToken.SecretName,
                        Key = gitToken.Key
                    }
                }
            });
        }
        return env;
    }

    private static List<V1Volume> BuildVolumes() =>
    [
        new V1Volume { Name = SharedVolume, EmptyDir = new V1EmptyDirVolumeSource() },
        new V1Volume { Name = WorkVolume, EmptyDir = new V1EmptyDirVolumeSource() }
    ];
}
