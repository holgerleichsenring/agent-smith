namespace AgentSmith.Contracts.Providers;

/// <summary>2026-11-23-c81ec: the live Deployment as Argo CD's resource endpoint answers it — the pod template's image
/// of container 'app' and its variables-checksum annotation, spec.replicas, metadata.generation,
/// status.observedGeneration and the ready and updated replica counts (absent counts read 0). Never
/// status.summary.images: it lists old ReplicaSets' images and is empty at 0 pods.</summary>
public sealed record ArgoLiveDeployment(
    string? TemplateImage, string? VariablesChecksum, int? Replicas, long Generation, long ObservedGeneration,
    int ReadyReplicas, int UpdatedReplicas);
