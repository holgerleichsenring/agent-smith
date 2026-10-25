using AgentSmith.Contracts.Sandbox;
using k8s.Models;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-10-25-c5a7a: what a sandbox pod's security profile adds to it. Hardened (the default):
/// uid and gid 1000, every capability dropped, a writable emptyDir at /root as HOME, and git told
/// that the root-owned /work is safe. Root (sandbox.run_as_root): the image's own user and the
/// runtime's default capabilities. Both run under RuntimeDefault seccomp with no privilege
/// escalation.
/// </summary>
public sealed class PodSecurity
{
    public const string HomeVolume = "agent-home";
    private const string RuntimeDefault = "RuntimeDefault";

    public V1PodSecurityContext Pod(SandboxSecurityContext profile) => profile.RunAsRoot
        ? new V1PodSecurityContext { FsGroup = profile.Gid, SeccompProfile = Seccomp() }
        : new V1PodSecurityContext
        {
            RunAsNonRoot = true,
            RunAsUser = profile.Uid,
            RunAsGroup = profile.Gid,
            FsGroup = profile.Gid,
            SeccompProfile = Seccomp()
        };

    public V1SecurityContext Container(SandboxSecurityContext profile) => new()
    {
        AllowPrivilegeEscalation = false,
        Capabilities = profile.RunAsRoot ? null : new V1Capabilities { Drop = ["ALL"] }
    };

    public IEnumerable<V1VolumeMount> HomeMounts(SandboxSecurityContext profile) => profile.RunAsRoot
        ? []
        : [new V1VolumeMount { Name = HomeVolume, MountPath = SandboxSecurityContext.Home }];

    public IEnumerable<V1Volume> HomeVolumes(SandboxSecurityContext profile) => profile.RunAsRoot
        ? []
        : [new V1Volume { Name = HomeVolume, EmptyDir = new V1EmptyDirVolumeSource() }];

    public IEnumerable<V1EnvVar> Env(SandboxSecurityContext profile) =>
        profile.ToolchainEnv().Select(kv => new V1EnvVar { Name = kv.Key, Value = kv.Value });

    private static V1SeccompProfile Seccomp() => new() { Type = RuntimeDefault };
}
