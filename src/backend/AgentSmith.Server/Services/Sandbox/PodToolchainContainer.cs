using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using k8s.Models;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992b: builds one toolchain container of a sandbox pod — its own agent (job id, bus URL), the declared
/// secret env and files, /shared read-only, the pod's one /work and, for a hardened profile, its own home volume.
/// Moved out of <see cref="PodSpecBuilder"/>, which now places one of these per toolchain.
/// </summary>
public sealed class PodToolchainContainer(PodSecurity podSecurity)
{
    public const string SharedVolume = "agent-shared";
    public const string WorkVolume = "agent-work";
    public const string SharedMount = "/shared";
    public const string WorkMount = Domain.Entities.Repository.WorkspaceRoot;

    public V1Container Build(PodToolchain toolchain, int index, SandboxSpec spec, SandboxSecurityContext profile) => new()
    {
        Name = PodToolchain.ContainerName(index),
        Image = toolchain.Image,
        SecurityContext = podSecurity.Container(profile),
        Command = [$"{SharedMount}/agent"],
        // p0360b: --run-id arms the agent's run-alive idle guard — an idle sandbox
        // of a live run keeps waiting instead of self-terminating. Omitted for
        // runless sandboxes (probe/preflight), which keep the plain idle backstop.
        // 2026-10-02-35b2: the Redis URL travels in REDIS_URL only — a command line is readable by every process.
        Args = string.IsNullOrEmpty(spec.RunId)
            ? ["--job-id", toolchain.JobId]
            : ["--job-id", toolchain.JobId, "--run-id", spec.RunId],
        Env = [.. BuildEnv(toolchain, spec.Secrets), .. podSecurity.Env(profile)],
        VolumeMounts =
        [
            new V1VolumeMount { Name = SharedVolume, MountPath = SharedMount, ReadOnlyProperty = true },
            new V1VolumeMount { Name = WorkVolume, MountPath = WorkMount },
            .. SandboxSecretProjection.FileMounts(spec.Secrets?.Files),
            .. podSecurity.HomeMounts(profile, index)
        ],
        WorkingDir = WorkMount,
        Resources = BuildResources(toolchain.Resources)
    };

    // Both Requests and Limits are emitted: Requests so the pod survives namespaces
    // with a ResourceQuota that mandates limits.cpu + requests.cpu + memory variants,
    // Limits so the toolchain container has a hard CFS/OOM cap. Quantities are passed
    // through to Kubernetes verbatim from <see cref="ResourceLimits"/>.
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

    // 2026-11-25-9992d: a repository's own variables travel under a scoped name (ScopedSecretNames) the agent hands
    // only to that repository's commands; the project-wide ones keep their own names.
    private static List<V1EnvVar> BuildEnv(PodToolchain toolchain, ResolvedSandboxSecrets? secrets)
    {
        var env = new List<V1EnvVar>
        {
            new() { Name = "JOB_ID", Value = toolchain.JobId },
            new() { Name = "REDIS_URL", Value = toolchain.RedisUrl }
        };
        // 2026-10-02-5f89g: no pod-wide GIT_TOKEN — each git step carries its repo's own.
        foreach (var binding in secrets?.Env ?? [])
            env.Add(SandboxSecretProjection.EnvVar(binding.EnvName, binding.Source));
        foreach (var binding in secrets?.RepoEnv ?? [])
            env.Add(SandboxSecretProjection.EnvVar(
                ScopedSecretNames.For(binding.Repository!, binding.EnvName), binding.Source));
        return env;
    }
}
