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
}
