namespace AgentSmith.Infrastructure.Services.Providers.Registry;

/// <summary>
/// 2026-11-23-933de: the manifest media types every HEAD accepts. The Docker schema2 manifest and manifest list sit
/// beside the OCI ones, because a classic 'docker push' stores schema2 and a registry answers its push digest in
/// Docker-Content-Digest only when Accept names that type.
/// </summary>
public static class OciMediaTypes
{
    public const string OciIndex = "application/vnd.oci.image.index.v1+json";
    public const string OciManifest = "application/vnd.oci.image.manifest.v1+json";
    public const string DockerManifest = "application/vnd.docker.distribution.manifest.v2+json";
    public const string DockerManifestList = "application/vnd.docker.distribution.manifest.list.v2+json";

    public static IReadOnlyList<string> Accepted { get; } = [OciIndex, OciManifest, DockerManifest, DockerManifestList];
}
