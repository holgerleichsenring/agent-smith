using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models.Sandbox;

/// <summary>
/// 2026-10-12-6bfab: the sandbox image boundary one decision is made against — which
/// registries are trusted, whether that list is the built-in default, and whether the Docker
/// Hub official library shape is trusted and whether the operator said so or it follows the
/// list. Built from one read of <see cref="SandboxGlobalConfig"/>, so the verdict, the warning
/// and the dashboard's "in force" panel all describe the same boundary.
/// <para>
/// Both the image and every entry are compared in the form Docker resolves them to: a
/// reference whose first segment is not a host lives on <c>docker.io</c>, and a repository
/// without a namespace lives under <c>docker.io/library/</c>. So <c>sbtscala/</c> and
/// <c>docker.io/sbtscala/</c> trust the same images. The canonical form is for matching only.
/// </para>
/// </summary>
public sealed record RegistryTrustBoundary(
    IReadOnlyList<string> Registries,
    bool RegistriesAreDefault,
    bool DockerHubLibrary,
    bool DockerHubLibraryIsExplicit)
{
    /// <summary>The registries trusted when the operator names none.</summary>
    public static readonly IReadOnlyList<string> DefaultRegistries = ["mcr.microsoft.com/", "ghcr.io/"];

    /// <summary>The boundary when the stored one could not be read and none was read before:
    /// it trusts nothing, so a declared image falls through to the catalog.</summary>
    public static readonly RegistryTrustBoundary Closed = new([], false, false, true);

    private const string DockerHub = "docker.io/";
    private const string Library = DockerHub + "library/";

    /// <summary>The boundary a sandbox block describes: named entries replace the default,
    /// and the library shape follows the list unless the switch is set.</summary>
    public static RegistryTrustBoundary From(SandboxGlobalConfig? sandbox)
    {
        IReadOnlyList<string> named = [.. (sandbox?.AllowedRegistries ?? [])
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => Canonical(Entry(r.Trim())))
            .Distinct(StringComparer.Ordinal)];
        var library = sandbox?.AllowDockerHubLibrary;
        return new RegistryTrustBoundary(
            named.Count > 0 ? named : DefaultRegistries, named.Count == 0,
            library ?? named.Count == 0, library is not null);
    }

    /// <summary>Is this image inside the boundary?</summary>
    public bool Accepts(string? image)
    {
        var trimmed = image?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return false;
        var canonical = Canonical(trimmed);
        if (Registries.Any(r => canonical.StartsWith(r, StringComparison.Ordinal))) return true;
        return DockerHubLibrary && IsLibraryShape(canonical);
    }

    /// <summary>Where the boundary runs, so a refusal tells the operator what to widen.</summary>
    public string Description =>
        Registries.Count == 0 && !DockerHubLibrary
            ? "no image — the stored boundary could not be read yet"
            : string.Join(", ", Registries)
              + (DockerHubLibrary ? ", or a Docker Hub official library image" : string.Empty)
              + " (sandbox.allowed_registries / sandbox.allow_docker_hub_library)";

    private static string Entry(string entry) => entry.EndsWith('/') ? entry : entry + "/";

    // Docker's reading of a reference: no '/' at all is an official library repository; a
    // first segment with a '.' or ':' (or 'localhost') is a registry host; anything else is a
    // namespace on Docker Hub. docker.io/<name> is docker.io/library/<name>.
    private static string Canonical(string reference)
    {
        var slash = reference.IndexOf('/');
        if (slash < 0) return Library + reference;
        var first = reference[..slash];
        if (!IsHost(first)) return DockerHub + reference;
        if (first != "docker.io") return reference;
        var rest = reference[(slash + 1)..];
        return rest.Length > 0 && !rest.Contains('/') ? Library + rest : reference;
    }

    private static bool IsHost(string segment) =>
        segment.Contains('.') || segment.Contains(':') || segment == "localhost";

    // The library shape is docker.io/library/<name>[:tag|@digest] with no further namespace.
    // The repository ends at the ':' that follows the last '/', so a port is never a tag.
    private static bool IsLibraryShape(string canonical)
    {
        if (!canonical.StartsWith(Library, StringComparison.Ordinal)) return false;
        var name = canonical[Library.Length..];
        var tag = name.IndexOf(':', name.LastIndexOf('/') + 1);
        return !(tag < 0 ? name : name[..tag]).Contains('/');
    }
}
