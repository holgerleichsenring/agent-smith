namespace AgentSmith.Server.Services.Sandbox;

public sealed class DockerSandboxOptions
{
    public string RedisUrl { get; set; } = "redis:6379";
    public string DockerSocketUri { get; set; } = "unix:///var/run/docker.sock";

    /// <summary>
    /// Docker network for sandbox containers. Empty triggers auto-detection
    /// (inspect own container by hostname, take its first network) which keeps
    /// the sandbox reachable from the server pod's redis hostname. Set
    /// explicitly via DOCKER_NETWORK when running outside Compose / K8s.
    /// </summary>
    public string Network { get; set; } = "";

    /// <summary>
    /// p0269a: maximum number of sandbox containers allowed to run concurrently on
    /// this Docker host. The capacity probe defers a new run while this many are
    /// already running, so tickets are processed sequentially instead of
    /// over-subscribing a limitless daemon (which would OOM-kill later, not reject
    /// at create). 0 = unbounded (historic behaviour). Set via
    /// SANDBOX_MAX_CONCURRENT. Default 2 — a conservative single-host floor.
    /// </summary>
    public int MaxConcurrentSandboxes { get; set; } = 2;
}
