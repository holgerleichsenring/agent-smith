using AgentSmith.Contracts.Sandbox;
using k8s.Models;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// Builds the V1Pod spec for a sandbox: the agent-loader initContainer and one toolchain container per
/// <see cref="PodToolchain"/>, sharing /shared (binary) and /work (working dir) emptyDir volumes.
/// 2026-11-25-9992b: a run's workspace pod carries a container per image, a single sandbox's pod one. The containers
/// share no process namespace (shareProcessNamespace stays unset), so no agent sees another's processes.
/// The security profile (who it runs as, capabilities, the /root home) is <see cref="PodSecurity"/>'s.
/// </summary>
public sealed class PodSpecBuilder(SandboxPodLabels labels, PodSecurity podSecurity)
{
    private const string SharedVolume = PodToolchainContainer.SharedVolume;
    private const string SharedMount = PodToolchainContainer.SharedMount;
    private readonly PodToolchainContainer _containers = new(podSecurity);

    /// <summary>A single sandbox's pod: one toolchain container from <paramref name="spec"/>'s image and limits.</summary>
    public V1Pod Build(string podName, string jobId, string redisUrl, SandboxSpec spec, V1OwnerReference? owner) =>
        Build(podName, [new PodToolchain(spec.ToolchainImage, spec.Resources, jobId, redisUrl)], spec, owner);

    /// <summary>
    /// A pod with a container per toolchain. <paramref name="spec"/> carries what they share (agent image, profile,
    /// run, secrets, pull secrets); its own image and limits are not read.
    /// </summary>
    public V1Pod Build(
        string podName, IReadOnlyList<PodToolchain> toolchains, SandboxSpec spec, V1OwnerReference? owner)
    {
        if (toolchains.Count == 0) throw new ArgumentException("A sandbox pod needs a toolchain.", nameof(toolchains));
        var profile = spec.SecurityContext ?? SandboxSecurityContext.Unconfined;
        return new V1Pod
        {
            Metadata = BuildMetadata(podName, toolchains, spec, owner),
            Spec = new V1PodSpec
            {
                RestartPolicy = "Never",
                SecurityContext = podSecurity.Pod(profile),
                AutomountServiceAccountToken = podSecurity.AutomountToken(profile),
                ImagePullSecrets = PullSecrets(spec.ImagePullSecrets),
                InitContainers = [BuildInitContainer(spec.AgentImage, profile)],
                Containers = [.. toolchains.Select((t, i) => _containers.Build(t, i, spec, profile))],
                Volumes =
                [
                    .. BuildVolumes(spec.Secrets?.Files),
                    .. toolchains.SelectMany((_, i) => podSecurity.HomeVolumes(profile, i))
                ]
            }
        };
    }

    // 2026-11-25-9992b: the pipeline-id label names the first container's job; the annotation names every one, so a
    // reaper deletes each container's bus credential.
    private V1ObjectMeta BuildMetadata(
        string podName, IReadOnlyList<PodToolchain> toolchains, SandboxSpec spec, V1OwnerReference? owner) => new()
    {
        Name = podName,
        Labels = labels.Build(toolchains[0].JobId, spec.RunId, spec.ConversationId),
        Annotations = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SandboxPodLabels.JobIdsAnnotation] = SandboxPodLabels.JoinJobIds(toolchains.Select(t => t.JobId))
        },
        OwnerReferences = owner is null ? null : [owner]
    };

    // 2026-08-31-46d7: pull secrets sit at POD level, so one declaration covers the
    // agent-loader init container as well as the toolchain. Null (not an empty list)
    // when none is declared, so an unconfigured pod stays exactly what it was.
    private static List<V1LocalObjectReference>? PullSecrets(IReadOnlyList<string>? names) =>
        names is null or [] ? null : [.. names.Select(name => new V1LocalObjectReference(name))];

    private V1Container BuildInitContainer(string agentImage, SandboxSecurityContext profile) => new()
    {
        Name = "agent-loader",
        Image = agentImage,
        SecurityContext = podSecurity.Container(profile),
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

    private static List<V1Volume> BuildVolumes(IReadOnlyList<SecretFileMount>? files) =>
    [
        new V1Volume { Name = SharedVolume, EmptyDir = new V1EmptyDirVolumeSource() },
        new V1Volume { Name = PodToolchainContainer.WorkVolume, EmptyDir = new V1EmptyDirVolumeSource() },
        .. SandboxSecretProjection.FileVolumes(files)
    ];
}
