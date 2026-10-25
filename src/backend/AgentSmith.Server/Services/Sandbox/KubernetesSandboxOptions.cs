using k8s.Models;

namespace AgentSmith.Server.Services.Sandbox;

public sealed class KubernetesSandboxOptions
{
    /// <summary>The namespace sandbox pods live in (2026-10-25-c5a7c: K8S_SANDBOX_NAMESPACE, else the server's).</summary>
    public string Namespace { get; set; } = "default";

    /// <summary>2026-10-25-c5a7c: the server's own namespace (K8S_NAMESPACE).</summary>
    public string ServerNamespace { get; set; } = "default";

    /// <summary>The Redis URL a sandbox is handed (2026-10-25-c5a7c: SANDBOX_REDIS_URL, else REDIS_URL).</summary>
    public string RedisUrl { get; set; } = "redis:6379";

    /// <summary>2026-10-25-c5a7c: the server's own REDIS_URL, which the owner identity is derived from.</summary>
    public string OwnerRedisUrl { get; set; } = "redis:6379";

    /// <summary>2026-10-25-c5a7c: whether SANDBOX_REDIS_URL was set.</summary>
    public bool HasSandboxRedisUrl { get; set; }

    public V1OwnerReference? OwnerReference { get; set; }

    /// <summary>2026-10-25-c5a7g: the proxy sandbox traffic goes through (SANDBOX_EGRESS_PROXY), or null.</summary>
    public SandboxEgressProxy? EgressProxy { get; set; }

    /// <summary>
    /// 2026-10-25-c5a7c: why sandboxes cannot be spawned with these options, or null. Pods in another
    /// namespace cannot resolve the short Redis service name, so they need SANDBOX_REDIS_URL.
    /// </summary>
    public string? Misconfiguration => Namespace != ServerNamespace && !HasSandboxRedisUrl
        ? $"Sandbox pods run in namespace '{Namespace}' and the server in '{ServerNamespace}', but "
          + "SANDBOX_REDIS_URL is not set. A pod in another namespace cannot resolve the server's short "
          + "Redis name; set SANDBOX_REDIS_URL to the Redis service's full name, such as "
          + $"redis.{ServerNamespace}.svc.cluster.local:6379."
        : null;
}
