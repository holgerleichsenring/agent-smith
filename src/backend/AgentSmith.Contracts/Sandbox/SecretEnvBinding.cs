namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// p0272: a resolved sandbox env var sourced from a Kubernetes Secret. The pod
/// builder turns this into a <c>secretKeyRef</c> env entry, so the value is
/// resolved by Kubernetes in the pod and never travels in a Step payload.
/// 2026-11-25-9992d: <paramref name="Repository"/> names the one repository whose commands receive it; null is
/// project-wide.
/// </summary>
public sealed record SecretEnvBinding(string EnvName, SecretRef Source, string? Repository = null);
