namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-08-28-b630: optional marker — this sandbox's backend PROJECTED the run's declared
/// credentials into it, and names what it projected.
/// <para>
/// Only the Kubernetes backend does: the pod builder is the sole consumer of
/// <see cref="SandboxSpec.Secrets"/>, while the docker and in-process sandboxes carry no
/// secret handling at all. Their absence from this interface is therefore the difference
/// between "the credential did not arrive" and "this backend injects none" — a distinction
/// without which every docker-tier run of a repository that declares a credential would go
/// red for a reason that has nothing to do with the repository.
/// </para>
/// </summary>
public interface ISandboxSecretInjection
{
    /// <summary>What the backend actually projected into this sandbox. Env NAMES and mount
    /// PATHS only — the values live in the operator's Secrets and are never read here.</summary>
    ResolvedSandboxSecrets InjectedSecrets { get; }
}
