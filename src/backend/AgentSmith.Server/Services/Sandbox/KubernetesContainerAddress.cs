namespace AgentSmith.Server.Services.Sandbox;

/// <summary>2026-11-25-9992b: where one toolchain container lives — its namespace, its pod and its name in the pod.</summary>
public sealed record KubernetesContainerAddress(string Namespace, string PodName, string Container);
