using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.Options;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-08-25-014d: whether a toolchain image comes from a source the operator
/// trusts. The image string is model-authored (context.yaml stack.image) or
/// catalog-authored (a domain profile), so this is the supply-chain boundary the
/// run is held to — and which registries sit inside it is the operator's to say,
/// through <see cref="SandboxGlobalConfig.AllowedRegistries"/>.
/// <para>
/// Two rules, deliberately separate. A REGISTRY is a reference prefix. The Docker
/// Hub official library namespace is not one — it is a repository SHAPE, an image
/// with no namespace segment at all — so it has its own switch. Expressed as a
/// registry entry it would admit every user repository on that host.
/// </para>
/// </summary>
public sealed class ImageRegistryTrust
{
    /// <summary>The registries trusted when the operator names none.</summary>
    public static readonly IReadOnlyList<string> DefaultRegistries =
        ["mcr.microsoft.com/", "ghcr.io/"];

    private readonly IReadOnlyList<string> _registries;
    private readonly bool _libraryNamespace;

    public ImageRegistryTrust(IOptions<SandboxGlobalConfig>? config = null)
    {
        var named = Named(config?.Value.AllowedRegistries);
        _registries = named.Count > 0 ? named : DefaultRegistries;
        _libraryNamespace = config?.Value.AllowDockerHubLibrary ?? named.Count == 0;
    }

    /// <summary>Is this image inside the boundary?</summary>
    public bool Accepts(string? image)
    {
        var trimmed = image?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return false;
        if (_registries.Any(r => trimmed.StartsWith(r, StringComparison.Ordinal))) return true;
        return _libraryNamespace && !Repository(trimmed).Contains('/', StringComparison.Ordinal);
    }

    /// <summary>Where the boundary runs, so a refusal tells the operator what to widen.</summary>
    public string Description =>
        string.Join(", ", _registries)
        + (_libraryNamespace ? ", or a Docker Hub official library image" : string.Empty)
        + " (sandbox.allowed_registries / sandbox.allow_docker_hub_library)";

    private static IReadOnlyList<string> Named(IEnumerable<string>? configured) =>
        [.. (configured ?? [])
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Select(r => r.EndsWith('/') ? r : r + "/")];

    // The repository is everything before the TAG, and the tag separator is the ':'
    // that follows the last '/'. Cutting at the first ':' instead would read the port
    // of `a.host:5000/pwn:latest` as a tag and the namespace-free remainder as an
    // official library image — a hole this rule cannot afford.
    private static string Repository(string image)
    {
        var tag = image.IndexOf(':', image.LastIndexOf('/') + 1);
        return tag < 0 ? image : image[..tag];
    }
}
