using AgentSmith.Application.Models;
using AgentSmith.Application.Models.Sandbox;
using AgentSmith.Domain.Models;

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

    // 2026-10-12-50bda: a boundary that could not be read trusts nothing; widening the list
    // would not help, reading it would.
    private const string UnreadLever =
        "the stored sandbox settings could not be read, so no image is trusted until they are — "
        + "check the configuration store, then run again";

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
            ReferenceEquals(boundary, RegistryTrustBoundary.Closed) ? UnreadLever : Lever(trimmed));
    }

    /// <summary>
    /// 2026-11-16-f3fba: the refusal of <paramref name="image"/> as data — the control on the Sandbox
    /// settings page and the value that lets the image in. A library image is let in by trusting the
    /// library shape; any other by the list with its registry prefix added, the defaults kept, because
    /// naming one entry replaces them. A boundary that could not be read names no control.
    /// </summary>
    public EndDetail Detail(string image, string context, string repo)
    {
        var subject = $"Image {image} (context {context})";
        var boundary = trust.InForce();
        if (ReferenceEquals(boundary, RegistryTrustBoundary.Closed)) return new EndDetail(subject, [repo]);
        var prefix = Prefix(image);
        return prefix is null
            ? new EndDetail(subject, [repo], LibraryControl, "Trust", SettingsPath)
            : new EndDetail(subject, [repo], RegistriesControl,
                string.Join(", ", boundary.Registries.Append(prefix).Distinct(StringComparer.Ordinal)), SettingsPath);
    }

    private const string LibraryControl = "Docker Hub official library images";
    private const string RegistriesControl = "Trusted image registries";
    private const string SettingsPath = "/config/settings/sandbox";

    private static string? Prefix(string image)
    {
        var repository = Repository(image.Trim());
        var slash = repository.LastIndexOf('/');
        return slash < 0 ? null : repository[..(slash + 1)];
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
