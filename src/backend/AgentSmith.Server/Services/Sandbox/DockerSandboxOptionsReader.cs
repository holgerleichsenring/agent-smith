namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-10-25-c5a7b: reads the Docker sandbox backend's server environment into
/// <see cref="DockerSandboxOptions"/>. Taken out of the registrations so their file stays one
/// responsibility; a new deployment-topology variable is read here.
/// </summary>
public sealed class DockerSandboxOptionsReader(Func<string, string?> environment)
{
    public DockerSandboxOptionsReader() : this(Environment.GetEnvironmentVariable)
    {
    }

    public DockerSandboxOptions Read() => new()
    {
        RedisUrl = environment("REDIS_URL") ?? "redis:6379",
        DockerSocketUri = environment("DOCKER_HOST") ?? DockerSocketUriResolver.DefaultSocket,
        Network = environment("DOCKER_NETWORK") ?? "",
        // p0407: warm package caches are what every run wants; the switch exists for
        // the operator who wants a provably cold restore or is short on disk.
        PackageCacheEnabled = !bool.TryParse(environment("SANDBOX_PACKAGE_CACHE"), out var cache) || cache,
        EgressProxy = SandboxEgressProxy.From(environment("SANDBOX_EGRESS_PROXY"))
    };
}
