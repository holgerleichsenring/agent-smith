using AgentSmith.Server.Models;
using Docker.DotNet;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentSmith.Server.Services;

/// <summary>
/// Resolves the Docker network for agent containers.
/// Priority: explicit config > auto-detect from hostname > fallback to bridge.
/// </summary>
internal sealed class DockerNetworkResolver(
    IOptions<JobSpawnerOptions> options,
    ILogger logger)
{
    private readonly JobSpawnerOptions _options = options.Value;

    public async Task<string> ResolveAsync(DockerClient client, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.DockerNetwork))
        {
            logger.LogDebug("Using configured Docker network: {Network}", _options.DockerNetwork);
            return _options.DockerNetwork;
        }

        try
        {
            var hostname = System.Net.Dns.GetHostName();
            var containerInfo = await client.Containers.InspectContainerAsync(hostname, cancellationToken);
            var network = containerInfo.NetworkSettings?.Networks?.Keys.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(network))
            {
                logger.LogDebug("Auto-detected Docker network: {Network}", network);
                return network;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug("Could not auto-detect Docker network: {Message}", ex.Message);
        }

        logger.LogDebug("Falling back to Docker network: bridge");
        return "bridge";
    }
}
