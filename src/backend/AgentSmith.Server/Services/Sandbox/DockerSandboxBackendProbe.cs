using System.Diagnostics;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Docker.DotNet;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// Can the Docker sandbox backend reach its daemon? Pings the client the backend creates its
/// containers through, so the answer is about the socket it really uses. Never throws.
/// </summary>
public sealed class DockerSandboxBackendProbe(
    IDockerClient docker,
    ILogger<DockerSandboxBackendProbe> logger) : IPreflightSandboxProbe
{
    public string BackendLabel => "Docker";

    public async Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await docker.System.PingAsync(cancellationToken);
            return ConnectionProbeResult.Reachable(stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Docker daemon probe failed");
            return ConnectionProbeResult.Unreachable(stopwatch.ElapsedMilliseconds, ex.Message);
        }
    }
}
