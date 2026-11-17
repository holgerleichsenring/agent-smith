using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models.Sandbox;

/// <summary>
/// 2026-10-12-6bfab: the sandbox image boundary one decision is made against — which
/// registries are trusted and whether that list is the built-in default. Built from one read of
/// <see cref="SandboxGlobalConfig"/>, so the verdict, the warning and the dashboard's "in force"
/// panel all describe the same boundary.
/// <para>
/// Both the image and every entry are compared in the form Docker resolves them to: a
/// reference whose first segment is not a host lives on <c>docker.io</c>, and a repository
/// without a namespace lives under <c>docker.io/library/</c>. So <c>sbtscala/</c> and
/// <c>docker.io/sbtscala/</c> trust the same images. The canonical form is for matching only.
/// </para>
/// <para>
/// 2026-11-16-f3fbd: the Docker Hub official library is the entry <c>docker.io/library/</c>, in the
/// default like the other two, instead of a switch beside the list.
/// </para>
/// </summary>
public sealed record RegistryTrustBoundary(IReadOnlyList<string> Registries, bool RegistriesAreDefault)
{
    private const string DockerHub = "docker.io/";
    private const string Library = DockerHub + "library/";

    /// <summary>The registries trusted when the operator names none.</summary>
    public static readonly IReadOnlyList<string> DefaultRegistries = ["mcr.microsoft.com/", "ghcr.io/", Library];

    /// <summary>The boundary when the stored one could not be read and none was read before:
    /// it trusts nothing, so a declared image falls through to the catalog.</summary>
    public static readonly RegistryTrustBoundary Closed = new([], false);

    /// <summary>The boundary a sandbox block describes: named entries replace the default.</summary>
    public static RegistryTrustBoundary From(SandboxGlobalConfig? sandbox)
    {
        IReadOnlyList<string> named = [.. (sandbox?.AllowedRegistries ?? [])
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => Canonical(Entry(r.Trim())))
            .Distinct(StringComparer.Ordinal)];
        return new RegistryTrustBoundary(named.Count > 0 ? named : DefaultRegistries, named.Count == 0);
    }

    /// <summary>Is this image inside the boundary?</summary>
    public bool Accepts(string? image)
    {
        var trimmed = image?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return false;
        var canonical = Canonical(trimmed);
        return Registries.Any(r => canonical.StartsWith(r, StringComparison.Ordinal));
    }

    /// <summary>Where the boundary runs, so a refusal tells the operator what to widen.</summary>
    public string Description =>
        Registries.Count == 0
            ? "no image — the stored boundary could not be read yet"
            : string.Join(", ", Registries) + " (sandbox.allowed_registries)";

    /// <summary>
    /// 2026-11-16-f3fbd: the entry that trusts <paramref name="image"/>'s registry — its repository in
    /// canonical form, up to the last '/': <c>docker.io/library/</c> for <c>node:22-bookworm</c>,
    /// <c>docker.io/sbtscala/</c> for <c>sbtscala/scala-sbt:1</c>.
    /// </summary>
    public static string PrefixOf(string image)
    {
        var repository = Repository(Canonical(image.Trim()));
        return repository[..(repository.LastIndexOf('/') + 1)];
    }

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

    // The repository is the reference without its digest and without the tag that follows
    // the last '/' — a registry port before it is not a tag.
    private static string Repository(string reference)
    {
        var digest = reference.IndexOf('@');
        var name = digest < 0 ? reference : reference[..digest];
        var tag = name.IndexOf(':', name.LastIndexOf('/') + 1);
        return tag < 0 ? name : name[..tag];
    }
}
