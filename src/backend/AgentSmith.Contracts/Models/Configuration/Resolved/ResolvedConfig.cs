namespace AgentSmith.Contracts.Models.Configuration.Resolved;

/// <summary>
/// p0270a: the materialized resolved config — the effective settings for every
/// configured project, the config-time desired-state the dashboard observes
/// (the k8s "kubectl get -o yaml" equivalent). Built once from the loaded
/// AgentSmithConfig via the single resolution pass.
/// </summary>
public sealed record ResolvedConfig(IReadOnlyDictionary<string, ResolvedProjectSettings> Projects);
