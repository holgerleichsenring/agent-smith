using AgentSmith.Application.Models.Sandbox;

namespace AgentSmith.Application.Services.Sandbox.Registry;

/// <summary>
/// 2026-10-12-50bdb: whether a registry has an image, asked with a manifest HEAD (Docker Hub
/// does not count a HEAD against the anonymous pull limit). Only a 404 is "missing": a
/// private or unknown repository answers 401/403, and that — like a rate limit, a timeout or
/// a host not looked up — says nothing about the tag. A missing tag comes with the closest
/// tags the repository does list.
/// </summary>
public sealed class ImageReferenceCheck(
    RegistryImageReferenceParser parser, RegistryClient client,
    RegistryTagLister lister, NearestTagRanker ranker) : IImageReferenceCheck
{
    // Every manifest shape: a registry answers 404 for an OCI index or a Docker list the
    // request does not accept, which would read as "missing" for every multi-arch image.
    public const string ManifestAccept =
        "application/vnd.oci.image.index.v1+json, application/vnd.oci.image.manifest.v1+json, "
        + "application/vnd.docker.distribution.manifest.list.v2+json, "
        + "application/vnd.docker.distribution.manifest.v2+json";

    private const int Found = 200;
    private const int NotFound = 404;

    public async Task<ImageReferenceVerdict> CheckAsync(string image, CancellationToken cancellationToken)
    {
        if (parser.Parse(image) is not { } reference)
            return ImageReferenceVerdict.Unverified(image, "it is not a reference the registry API can address");
        var reply = await client.SendAsync(HttpMethod.Head, reference.Manifest, ManifestAccept, null, cancellationToken);
        return reply.Status switch
        {
            Found => ImageReferenceVerdict.Exists(image),
            NotFound => await MissingAsync(reference, reply.Bearer, cancellationToken),
            _ => ImageReferenceVerdict.Unverified(image, Unanswered(reference, reply)),
        };
    }

    private async Task<ImageReferenceVerdict> MissingAsync(
        RegistryImageReference reference, string? bearer, CancellationToken cancellationToken)
    {
        var detail = $"{reference.ApiHost} has no '{reference.Reference}' in repository {reference.Repository}";
        var tags = await lister.ListAsync(reference, bearer, cancellationToken);
        // A digest names no tag to be close to: the list was read, nothing is offered.
        IReadOnlyList<string>? candidates = tags is null ? null
            : reference.IsDigest ? [] : ranker.Nearest(reference.Reference, tags);
        return ImageReferenceVerdict.Missing(reference.Image, detail, candidates);
    }

    private static string Unanswered(RegistryImageReference reference, RegistryReply reply) =>
        reply.Failure ?? reply.Status switch
        {
            401 or 403 => $"{reference.ApiHost} asks for credentials for {reference.Repository} (HTTP {reply.Status})",
            429 => $"{reference.ApiHost} is rate-limiting this server (HTTP 429)",
            _ => $"{reference.ApiHost} answered HTTP {reply.Status}",
        };
}
