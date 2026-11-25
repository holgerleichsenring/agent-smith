namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-11-25-9992b: a run's workspace — one container per <see cref="Toolchains"/> image, every one mounting the
/// same /work. <see cref="Base"/> carries what all containers share (agent image, security profile, run id, step cap,
/// declared secrets, pull secrets, timeouts); its own ToolchainImage and Resources are not read.
/// <see cref="HostSources"/> maps a repository name to a host directory the in-process backend serves at
/// /work/&lt;repo&gt;; container backends clone instead.
/// </summary>
public sealed record WorkspaceSpec(
    SandboxSpec Base,
    IReadOnlyList<WorkspaceToolchain> Toolchains,
    IReadOnlyDictionary<string, string>? HostSources = null);
