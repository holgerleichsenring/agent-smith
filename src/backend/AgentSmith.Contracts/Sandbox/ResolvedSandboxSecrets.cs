namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// p0272: the validated, parsed form of the operator's <c>sandbox.secrets</c>
/// block — what <see cref="SandboxSpec"/> carries and the pod builder consumes.
/// Parsing and fail-fast validation happen once, in the resolver, so by the time
/// a spec holds this the references are well-formed.
/// </summary>
public sealed record ResolvedSandboxSecrets(
    IReadOnlyList<SecretEnvBinding> Env,
    IReadOnlyList<SecretFileMount> Files)
{
    public static readonly ResolvedSandboxSecrets Empty = new([], []);
}
