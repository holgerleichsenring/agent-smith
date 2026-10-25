namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-10-25-c5a7c: reads the Kubernetes sandbox backend's server environment. Both new
/// variables fall back to what the server used before: K8S_SANDBOX_NAMESPACE to K8S_NAMESPACE,
/// SANDBOX_REDIS_URL to REDIS_URL, so a deployment that sets neither behaves as it did.
/// </summary>
public sealed class KubernetesSandboxOptionsReader(Func<string, string?> environment)
{
    private const string DefaultNamespace = "default";
    private const string DefaultRedisUrl = "redis:6379";

    public KubernetesSandboxOptionsReader() : this(Environment.GetEnvironmentVariable)
    {
    }

    public KubernetesSandboxOptions Read()
    {
        var serverNamespace = Value("K8S_NAMESPACE") ?? DefaultNamespace;
        var redisUrl = Value("REDIS_URL") ?? DefaultRedisUrl;
        var sandboxRedisUrl = Value("SANDBOX_REDIS_URL");
        return new KubernetesSandboxOptions
        {
            ServerNamespace = serverNamespace,
            Namespace = Value("K8S_SANDBOX_NAMESPACE") ?? serverNamespace,
            OwnerRedisUrl = redisUrl,
            RedisUrl = sandboxRedisUrl ?? redisUrl,
            HasSandboxRedisUrl = sandboxRedisUrl is not null
        };
    }

    private string? Value(string name) => environment(name) is { Length: > 0 } value ? value : null;
}
