using Docker.DotNet;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// The Docker network a sandbox's containers join: the configured one, else the server's own container's, else
/// bridge. Without this the sandbox lands on Docker's default bridge with no DNS for `redis` /
/// `host.docker.internal`, and the sandbox-agent's Redis connect times out with a confusing UnableToConnect.
/// 2026-11-25-9992b: moved out of the sandbox factory so the workspace factory resolves it the same way.
/// </summary>
public sealed class DockerSandboxNetwork(IDockerClient docker, DockerSandboxOptions options, ILogger logger)
{
    public async Task<string> ResolveAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(options.Network))
        {
            logger.LogDebug("Using configured sandbox network: {Network}", options.Network);
            return options.Network;
        }
        try
        {
            var hostname = System.Net.Dns.GetHostName();
            var info = await docker.Containers.InspectContainerAsync(hostname, ct);
            var network = info.NetworkSettings?.Networks?.Keys.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(network))
            {
                logger.LogDebug("Auto-detected sandbox network: {Network}", network);
                return network;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug("Could not auto-detect sandbox network: {Message}", ex.Message);
        }
        logger.LogDebug("Falling back to bridge network for sandbox");
        return "bridge";
    }
}
