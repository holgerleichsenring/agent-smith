namespace AgentSmith.Server.Services.Sandbox;

public sealed class DockerSandboxOptions
{
    public string RedisUrl { get; set; } = "redis:6379";
    public string DockerSocketUri { get; set; } = "unix:///var/run/docker.sock";
}
