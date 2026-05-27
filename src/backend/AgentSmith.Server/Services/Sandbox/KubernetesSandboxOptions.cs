using k8s.Models;

namespace AgentSmith.Server.Services.Sandbox;

public sealed class KubernetesSandboxOptions
{
    public string Namespace { get; set; } = "default";
    public string RedisUrl { get; set; } = "redis:6379";
    public V1OwnerReference? OwnerReference { get; set; }
}
