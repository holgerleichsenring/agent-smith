namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// p0272: the validated, parsed form of the operator's <c>sandbox.secrets</c>
/// block — what <see cref="SandboxSpec"/> carries and the pod builder consumes.
/// Parsing and fail-fast validation happen once, in the resolver, so by the time
/// a spec holds this the references are well-formed.
/// 2026-11-25-9992d: <see cref="RepoEnv"/> holds the repository-scoped variables, each binding naming its
/// repository; <see cref="Env"/> stays the project-wide ones.
/// </summary>
public sealed record ResolvedSandboxSecrets(
    IReadOnlyList<SecretEnvBinding> Env,
    IReadOnlyList<SecretFileMount> Files,
    IReadOnlyList<SecretEnvBinding>? RepoEnv = null)
{
    public static readonly ResolvedSandboxSecrets Empty = new([], []);

    public IReadOnlyList<SecretEnvBinding> RepoEnv { get; init; } = RepoEnv ?? [];

    /// <summary>Every declaration the pod carries: project-wide variables, files and scoped variables.</summary>
    public int Count => Env.Count + Files.Count + RepoEnv.Count;
}
