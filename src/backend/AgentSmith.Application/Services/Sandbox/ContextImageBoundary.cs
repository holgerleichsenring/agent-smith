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
        + "replaces the built-in defaults, so list everything still needed), or set "
        + "sandbox.allow_new_registries so a code run adds it and records that it did";

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
    /// settings page and the value that lets the image in: the list with the image's registry prefix
    /// added, the defaults kept, because naming one entry replaces them. 2026-11-16-f3fbd: a library
    /// image's prefix is the entry docker.io/library/. A boundary that could not be read names no control.
    /// </summary>
    public EndDetail Detail(string image, string context, string repo)
    {
        var subject = $"{SubjectPrefix}{image}{ContextInfix}{context})";
        var boundary = trust.InForce();
        if (ReferenceEquals(boundary, RegistryTrustBoundary.Closed)) return new EndDetail(subject, [repo]);
        var widened = boundary.Registries.Append(RegistryTrustBoundary.PrefixOf(image)).Distinct(StringComparer.Ordinal);
        return new EndDetail(subject, [repo], RegistriesControl, string.Join(", ", widened), SettingsPath);
    }

    /// <summary>
    /// 2026-11-16-f3fbc: the image a refusal's detail names, read back from the subject <see cref="Detail"/> wrote;
    /// null when the detail is not an image refusal's. Whether it is still refused is <see cref="Judge"/>'s answer.
    /// </summary>
    public string? RefusedImage(EndDetail? detail)
    {
        var subject = detail?.Subject;
        if (subject is null || !subject.StartsWith(SubjectPrefix, StringComparison.Ordinal)) return null;
        var end = subject.LastIndexOf(ContextInfix, StringComparison.Ordinal);
        var image = end < SubjectPrefix.Length ? null : subject[SubjectPrefix.Length..end].Trim();
        return string.IsNullOrEmpty(image) ? null : image;
    }

    private const string SubjectPrefix = "Image ";
    private const string ContextInfix = " (context ";
    private const string RegistriesControl = "Trusted image registries";
    private const string SettingsPath = "/config/settings/sandbox";

    private static string Lever(string image) =>
        $"to use it, add '{RegistryTrustBoundary.PrefixOf(image)}' to sandbox.allowed_registries"
        + ListReplacesDefaults + ", " + ProjectLever;
}
