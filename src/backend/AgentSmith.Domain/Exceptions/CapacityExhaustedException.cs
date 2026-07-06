namespace AgentSmith.Domain.Exceptions;

/// <summary>
/// Thrown when a sandbox cannot be created because the target's capacity is
/// exhausted — a Kubernetes ResourceQuota rejecting the pod-create, or a
/// configured concurrent-sandbox cap being reached. It is a FIRST-CLASS capacity
/// signal raised at the spawner boundary (where the k8s status reason is already
/// structured), NOT a message-string heuristic in the core: callers branch on the
/// TYPE to requeue the run as waiting instead of terminal-failing it (p0269a).
/// </summary>
public sealed class CapacityExhaustedException : AgentSmithException
{
    /// <summary>The capacity scope that was full (e.g. the k8s namespace, or "docker-host").</summary>
    public string Scope { get; }

    /// <summary>The resource that ran out when known (e.g. "requests.cpu", "pods", "concurrent-sandboxes"); null when the rejection did not name one.</summary>
    public string? ExhaustedResource { get; }

    public CapacityExhaustedException(string scope, string? exhaustedResource, string message)
        : base(message)
    {
        Scope = scope;
        ExhaustedResource = exhaustedResource;
    }

    public CapacityExhaustedException(string scope, string? exhaustedResource, string message, Exception innerException)
        : base(message, innerException)
    {
        Scope = scope;
        ExhaustedResource = exhaustedResource;
    }
}
