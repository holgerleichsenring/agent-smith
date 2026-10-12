namespace AgentSmith.Application.Models.Sandbox;

/// <summary>
/// 2026-10-12-50bdb: an image reference in the shape the registry API addresses it — the
/// host that serves the API (Docker Hub's is not the host its names carry), the repository
/// and the tag or digest.
/// </summary>
public sealed record RegistryImageReference(string Image, string ApiHost, string Repository, string Reference)
{
    /// <summary>A tag never holds ':'; a digest always does ("sha256:…").</summary>
    public bool IsDigest => Reference.Contains(':');

    public Uri Manifest => new($"https://{ApiHost}/v2/{Repository}/manifests/{Reference}");

    public Uri TagList(int pageSize) => new($"https://{ApiHost}/v2/{Repository}/tags/list?n={pageSize}");
}
