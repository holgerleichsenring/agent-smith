using AgentSmith.Application.Models.Sandbox;

namespace AgentSmith.Application.Services.Sandbox.Registry;

/// <summary>
/// 2026-10-12-50bdb: reads an image reference the way Docker resolves it, into the address
/// the registry API serves it under. A first segment with a '.' or ':' (or 'localhost') is a
/// registry host; anything else lives on Docker Hub, whose API answers on registry-1.docker.io,
/// and a Docker Hub name without a namespace lives under library/. No tag means 'latest'.
/// The same reading as <see cref="Models.Sandbox.RegistryTrustBoundary"/>'s matching form.
/// </summary>
public sealed class RegistryImageReferenceParser
{
    private const string DockerHub = "docker.io";
    private const string LegacyDockerHub = "index.docker.io";
    private const string DockerHubApi = "registry-1.docker.io";
    private const string Library = "library/";
    private const string DefaultTag = "latest";

    /// <summary>Null when the text is not a reference the registry API can address.</summary>
    public RegistryImageReference? Parse(string? image)
    {
        var trimmed = image?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Any(char.IsWhiteSpace)) return null;
        var (name, reference) = SplitReference(trimmed);
        var (host, repository) = SplitHost(name);
        return repository.Length == 0 || reference.Length == 0 || repository.EndsWith('/')
            ? null
            : new RegistryImageReference(trimmed, host, repository, reference);
    }

    // A digest wins over a tag; the tag is the ':' after the last '/', so a port is never one.
    private static (string Name, string Reference) SplitReference(string image)
    {
        var at = image.IndexOf('@');
        var name = at < 0 ? image : image[..at];
        var colon = name.IndexOf(':', name.LastIndexOf('/') + 1);
        var untagged = colon < 0 ? name : name[..colon];
        if (at >= 0) return (untagged, image[(at + 1)..]);
        return colon < 0 ? (name, DefaultTag) : (untagged, name[(colon + 1)..]);
    }

    private static (string Host, string Repository) SplitHost(string name)
    {
        var slash = name.IndexOf('/');
        var first = slash < 0 ? string.Empty : name[..slash];
        if (!IsHost(first)) return (DockerHubApi, OnDockerHub(name));
        var rest = name[(slash + 1)..];
        return first is DockerHub or LegacyDockerHub ? (DockerHubApi, OnDockerHub(rest)) : (first, rest);
    }

    private static string OnDockerHub(string repository) =>
        repository.Contains('/') || repository.Length == 0 ? repository : Library + repository;

    private static bool IsHost(string segment) =>
        segment.Contains('.') || segment.Contains(':') || segment == "localhost";
}
