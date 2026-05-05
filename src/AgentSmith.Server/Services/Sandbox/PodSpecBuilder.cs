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
        VolumeMounts = [new V1VolumeMount { Name = SharedVolume, MountPath = SharedMount }]
    };

    private static V1Container BuildToolchainContainer(SandboxSpec spec, string jobId, string redisUrl)
    {
        var resources = spec.Resources ?? new ResourceLimits();
        return new V1Container
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
            Resources = new V1ResourceRequirements
            {
                Limits = new Dictionary<string, ResourceQuantity>
                {
                    ["cpu"] = new(resources.CpuCores.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)),
                    ["memory"] = new(resources.Memory)
                }
            }
        };
    }

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
