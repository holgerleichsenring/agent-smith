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
    /// p0407: mount the persistent package-cache volumes into every sandbox so a
    /// restore reads packages from disk instead of the network. On by default —
    /// a cold restore cost a measured 978s (killed at the step cap) and 403s on
    /// one solution, while all 268 other sandbox commands of that run together
    /// took 1.5 minutes. Set SANDBOX_PACKAGE_CACHE=false to turn it off, e.g. to
    /// force a provably cold restore or to spare disk; the sandbox then behaves
    /// exactly as it did before this phase.
    /// </summary>
    public bool PackageCacheEnabled { get; set; } = true;
}
