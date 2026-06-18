namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// p0272: a resolved sandbox file mount sourced from a Kubernetes Secret. The pod
/// builder projects <see cref="Source"/>'s key as a single read-only file at
/// <see cref="MountPath"/> (via a Secret volume + subPath), so a build step can
/// read it by path without the value ever entering a Step payload.
/// </summary>
public sealed record SecretFileMount(string MountPath, SecretRef Source);
