using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0407: makes sure an image is on the Docker host before a container is created,
/// pulling it when it is not. Lifted out of DockerSandboxFactory — "which images
/// exist here" is a registry question, not a sandbox-lifecycle one.
/// </summary>
public sealed class DockerImagePresence(IDockerClient docker, ILogger<DockerImagePresence> logger)
{
    /// <summary>
    /// Honors IfNotPresent semantics universally. Toolchain images (alpine, node,
    /// python, dotnet/sdk, …) are pulled from Docker Hub on demand; the carrier
    /// agent image is typically locally-built, so a pull failure for it becomes a
    /// "build it first" message instead of a raw registry error.
    /// </summary>
    public async Task EnsurePresentAsync(string image, bool isCarrier, CancellationToken cancellationToken)
    {
        try
        {
            await docker.Images.InspectImageAsync(image, cancellationToken);
            return;
        }
        catch (DockerImageNotFoundException) { /* fall through to pull */ }

        var (repo, tag) = SplitImageRef(image);
        logger.LogInformation("Pulling image {Image} (not present locally)", image);
        try
        {
            await docker.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = repo, Tag = tag },
                authConfig: null, new Progress<JSONMessage>(), cancellationToken);
        }
        catch (DockerApiException ex) when (isCarrier)
        {
            throw new InvalidOperationException(
                $"Sandbox agent image '{image}' not found locally and not pullable from a registry. " +
                $"Build it once with: docker compose --profile build-only build sandbox-agent " +
                $"(or: docker build -t {image} -f src/AgentSmith.Sandbox.Agent/Dockerfile .)", ex);
        }
    }

    private static (string Repo, string Tag) SplitImageRef(string image)
    {
        var lastColon = image.LastIndexOf(':');
        // Guard against `host:port/repo` references where the colon belongs to
        // the registry, not a tag.
        if (lastColon < 0 || image.IndexOf('/', lastColon) >= 0)
            return (image, "latest");
        return (image[..lastColon], image[(lastColon + 1)..]);
    }
}
