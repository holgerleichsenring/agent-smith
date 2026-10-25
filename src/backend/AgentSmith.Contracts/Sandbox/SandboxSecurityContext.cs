namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-10-25-c5a7a: who a sandbox's processes run as. The default is the hardened profile:
/// uid and gid 1000, no capabilities. <see cref="RunAsRoot"/> is a project's named opt-out
/// (sandbox.run_as_root) for toolchains whose prerequisites need root, such as apt-get.
/// </summary>
public sealed record SandboxSecurityContext(bool RunAsRoot = false, long Uid = 1000, long Gid = 1000)
{
    /// <summary>The canonical sandbox home every credential path writes to.</summary>
    public const string Home = "/root";

    public static SandboxSecurityContext Hardened { get; } = new();

    public static SandboxSecurityContext Root { get; } = new(RunAsRoot: true);

    /// <summary>
    /// The toolchain env the profile needs. Hardened: HOME stays /root (uid 1000 has no passwd
    /// entry in most images), and git accepts the root-owned /work, which it would otherwise
    /// refuse as dubiously owned. Root: nothing, the image's own environment holds.
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
