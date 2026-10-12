using AgentSmith.Application.Models;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-10-12-6bfac: judges a context's stack.image against the registry boundary the
/// sandbox image chain holds it to, and words the refusal for the people who can act on it.
/// <para>
/// The chain drops a refused image with a server-log line and runs another, so a bootstrap
/// that wrote one learned nothing and the run found out from a missing binary half an hour
/// later. The same verdict is now spoken where the image is written and where it is refused.
/// </para>
/// </summary>
public sealed class ContextImageBoundary(IImageRegistryTrust trust)
{
    private const string ProjectLever =
        "or name an image in the project's Sandbox tab (sandbox.images for the language, or "
        + "sandbox.toolchain_image)";

    private const string ListReplacesDefaults =
        " (Settings > Sandbox > Trusted image registries, applied to the next sandbox; naming any entry "
        + "replaces the built-in defaults and turns off Docker Hub official images unless "
        + "sandbox.allow_docker_hub_library is set, so list everything still needed)";

    /// <summary>Null when the image is absent or inside the boundary.</summary>
    public ContextImageRefusal? Judge(string? image)
    {
        var trimmed = image?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        var boundary = trust.InForce();
        if (boundary.Accepts(trimmed)) return null;
        return new ContextImageRefusal(
            trimmed,
            $"stack.image '{trimmed}' is outside the trusted image registries "
            + $"[{boundary.Description}], so a sandbox will not run it",
            Lever(trimmed));
    }

    private static string Lever(string image)
    {
        var repository = Repository(image);
        var slash = repository.LastIndexOf('/');
        if (slash < 0)
            return "to use it, set sandbox.allow_docker_hub_library: true (Settings > Sandbox > "
                   + "Trusted image registries), " + ProjectLever;
        return $"to use it, add '{repository[..(slash + 1)]}' to sandbox.allowed_registries"
               + ListReplacesDefaults + ", " + ProjectLever;
    }

    // The repository is the reference without its digest and without the tag that follows
    // the last '/' — a registry port before it is not a tag.
    private static string Repository(string image)
    {
        var digest = image.IndexOf('@');
        var name = digest < 0 ? image : image[..digest];
        var tag = name.IndexOf(':', name.LastIndexOf('/') + 1);
        return tag < 0 ? name : name[..tag];
    }
}
