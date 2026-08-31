using Docker.DotNet;
using Docker.DotNet.Models;

namespace AgentSmith.Infrastructure.Services.Containers;

/// <summary>
/// Pulls a tool image when the local daemon does not already have it. One copy for both
/// Docker runners, which carried the same routine each.
/// </summary>
internal static class DockerImagePuller
{
    internal static async Task EnsureAsync(
        DockerClient client, string image, CancellationToken ct)
    {
        try
        {
            await client.Images.InspectImageAsync(image, ct);
        }
        catch (DockerImageNotFoundException)
        {
            // Not a swallow: the image is absent, and pulling it is the recovery.
            var parts = image.Split(':');
            await client.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = parts[0], Tag = parts.Length > 1 ? parts[1] : "latest" },
                null, new Progress<JSONMessage>(), ct);
        }
    }
}
