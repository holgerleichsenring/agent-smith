using AgentSmith.Contracts.Sandbox;
using k8s.Models;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-10-25-c5a7a: what a sandbox pod's security profile adds to it. Hardened (the default):
/// uid and gid 1000, every capability dropped, a writable emptyDir at /root as HOME, and git told
/// that the root-owned /work is safe. Root (sandbox.run_as_root): the image's own user and the
/// runtime's default capabilities. Both run under RuntimeDefault seccomp with no privilege
/// escalation, without the service-account token. Unconfined (2026-10-25-ff3b, hardening off):
/// the pod as it was before any of this, fsGroup only.
/// </summary>
public sealed class PodSecurity(KubernetesSandboxOptions options)
{
    public const string HomeVolume = "agent-home";
    private const string RuntimeDefault = "RuntimeDefault";

    public V1PodSecurityContext Pod(SandboxSecurityContext profile) => profile.RunAsRoot
        ? new V1PodSecurityContext { FsGroup = profile.Gid, SeccompProfile = profile.Confined ? Seccomp() : null }
        : new V1PodSecurityContext
        {
            RunAsNonRoot = true,
            RunAsUser = profile.Uid,
            RunAsGroup = profile.Gid,
            FsGroup = profile.Gid,
            SeccompProfile = Seccomp()
        };

    public V1SecurityContext? Container(SandboxSecurityContext profile) => !profile.Confined ? null : new()
    {
        AllowPrivilegeEscalation = false,
        Capabilities = profile.RunAsRoot ? null : new V1Capabilities { Drop = ["ALL"] }
    };

    /// <summary>False for a confined pod; null leaves the cluster's default, as an unconfined pod always had.</summary>
    public bool? AutomountToken(SandboxSecurityContext profile) => profile.Confined ? false : null;

    /// <summary>
    /// 2026-11-25-9992b: the home volume of the toolchain container at <paramref name="index"/> — one per container,
    /// so what one toolchain stages under ~ (a NuGet config, an .npmrc) stays its own.
    /// </summary>
    public static string HomeVolumeFor(int index) => $"{HomeVolume}-{index}";

    public IEnumerable<V1VolumeMount> HomeMounts(SandboxSecurityContext profile, int index) => profile.RunAsRoot
        ? []
        : [new V1VolumeMount { Name = HomeVolumeFor(index), MountPath = SandboxSecurityContext.Home }];

    public IEnumerable<V1Volume> HomeVolumes(SandboxSecurityContext profile, int index) => profile.RunAsRoot
        ? []
        : [new V1Volume { Name = HomeVolumeFor(index), EmptyDir = new V1EmptyDirVolumeSource() }];

    /// <summary>The profile's toolchain env, plus the egress proxy variables when one is set (2026-10-25-c5a7g).</summary>
    public IEnumerable<V1EnvVar> Env(SandboxSecurityContext profile) =>
        profile.ToolchainEnv().Concat(options.EgressProxy?.Env() ?? [])
            .Select(kv => new V1EnvVar { Name = kv.Key, Value = kv.Value });

    private static V1SeccompProfile Seccomp() => new() { Type = RuntimeDefault };
}
