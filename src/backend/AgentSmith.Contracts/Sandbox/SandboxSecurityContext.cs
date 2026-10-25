namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-10-25-c5a7a: who a sandbox's processes run as. <see cref="Hardened"/>: uid and gid 1000,
/// no capabilities. <see cref="Root"/>: a project's named opt-out (sandbox.run_as_root) for
/// prerequisites that need root, still confined (seccomp, no escalation, no token).
/// 2026-10-25-ff3b: hardening is an installation opt-in (sandbox.hardening_enabled); while it is
/// off every sandbox is <see cref="Unconfined"/>, the container it was before hardening existed,
/// which is also what a spec carrying no profile gets.
/// </summary>
public sealed record SandboxSecurityContext(
    bool RunAsRoot = false, long Uid = 1000, long Gid = 1000, bool Confined = true)
{
    /// <summary>The canonical sandbox home every credential path writes to.</summary>
    public const string Home = "/root";

    public static SandboxSecurityContext Hardened { get; } = new();

    public static SandboxSecurityContext Root { get; } = new(RunAsRoot: true);

    public static SandboxSecurityContext Unconfined { get; } = new(RunAsRoot: true, Confined: false);

    /// <summary>The profile an installation and a project resolve to.</summary>
    public static SandboxSecurityContext For(bool hardeningEnabled, bool projectRunsAsRoot) =>
        !hardeningEnabled ? Unconfined : projectRunsAsRoot ? Root : Hardened;

    /// <summary>
    /// The toolchain env the profile needs. Hardened: HOME stays /root (uid 1000 has no passwd
    /// entry in most images), and git accepts the root-owned /work, which it would otherwise
    /// refuse as dubiously owned. Root and unconfined: nothing, the image's own environment holds.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> ToolchainEnv() => RunAsRoot
        ? []
        :
        [
            new("HOME", Home),
            new("GIT_CONFIG_COUNT", "1"),
            new("GIT_CONFIG_KEY_0", "safe.directory"),
            new("GIT_CONFIG_VALUE_0", "*")
        ];
}
