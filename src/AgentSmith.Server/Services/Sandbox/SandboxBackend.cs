namespace AgentSmith.Server.Extensions;

/// <summary>
/// Sandbox backend kinds the Server can detect at startup. Resolution priority:
/// SANDBOX_TYPE explicit &gt; KUBERNETES_SERVICE_HOST &gt; /var/run/docker.sock
/// &gt; InProcess fallback.
/// </summary>
internal enum SandboxBackend
{
    InProcess,
    Kubernetes,
    Docker
}

internal sealed record SandboxBackendInfo(SandboxBackend Backend);
