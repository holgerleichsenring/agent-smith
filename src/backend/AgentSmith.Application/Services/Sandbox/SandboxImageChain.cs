using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0504: picks the one toolchain image a sandbox runs in. Order, most authoritative first:
/// sandbox.toolchain_image (operator, whole project), sandbox.images[lang] (operator, per
/// language, p0245), context.yaml stack.image (p0265, inside the registry boundary), the
/// language convention table, the generic git-bearing fallback.
/// <para>
/// 2026-08-25-014d: the only thing an image is judged by here is where it comes from. What it
/// CONTAINS is discovered where it is used — a sandbox clones into itself, so an image without
/// git fails at the checkout, by name.
/// </para>
/// <para>
/// 2026-10-12-50bda: <see cref="Decide"/> reports a refused stack.image as part of the
/// decision instead of dropping it, so the code run's preflight can stop on the substitution.
/// </para>
/// </summary>
public sealed class SandboxImageChain(
    IImageRegistryTrust? trust = null, ILogger<SandboxImageChain>? logger = null)
{
    private readonly IImageRegistryTrust _trust = trust ?? new ImageRegistryTrust();

    // Generic fallback: glibc (the self-contained agent binary is glibc-linked; musl crashes
    // exec with a misleading ENOENT) AND git on PATH (CheckoutSource). The Docker-official SCM
    // toolbox: Debian bookworm + git + ca-certs + curl. Operators override it through
    // ResolvedProject.Sandbox.ToolchainImage.
    public const string GenericFallbackImage = "buildpack-deps:bookworm-scm";

    /// <summary>The image and the link that chose it, without logging.</summary>
    public SandboxImageDecision Decide(
        ResolvedProject projectConfig, string? language, string? contextImage)
    {
        ArgumentNullException.ThrowIfNull(projectConfig);
        var projectOverride = projectConfig.Sandbox?.ToolchainImage;
        if (!string.IsNullOrEmpty(projectOverride))
            return new SandboxImageDecision(projectOverride, SandboxImageLink.ProjectToolchainImage);
        if (ConfiguredImage(projectConfig, language) is { } configured)
            return new SandboxImageDecision(configured, SandboxImageLink.LanguageImage);
        var declared = contextImage?.Trim();
        if (!string.IsNullOrEmpty(declared) && _trust.InForce().Accepts(declared))
            return new SandboxImageDecision(declared, SandboxImageLink.ContextImage);
        var refused = string.IsNullOrEmpty(declared) ? null : declared;
        return ToolchainImageCatalog.ForLanguage(language) is { } known
            ? new SandboxImageDecision(known, SandboxImageLink.Catalog, refused)
            : new SandboxImageDecision(GenericFallbackImage, SandboxImageLink.GenericFallback, refused);
    }

    /// <summary>The image a sandbox is started in; one log line names the deciding link.</summary>
    public string Resolve(ResolvedProject projectConfig, string? language, string? contextImage)
    {
        var decision = Decide(projectConfig, language, contextImage);
        if (decision.RefusedContextImage is { } refused) LogRefused(refused, language);
        if (decision.Link == SandboxImageLink.GenericFallback) LogFellThrough(language);
        else
            logger?.LogInformation(
                "Toolchain image '{Image}' for lang={Lang}, chosen by {Link}.",
                decision.Image, language ?? "null", LinkName(decision.Link));
        return decision.Image;
    }

    private static string LinkName(SandboxImageLink link) => link switch
    {
        SandboxImageLink.ProjectToolchainImage => "sandbox.toolchain_image",
        SandboxImageLink.LanguageImage => "sandbox.images",
        SandboxImageLink.ContextImage => "the context's stack.image",
        _ => "the language catalog",
    };

    // Neutral on purpose: this runs for every pipeline and backend, and only the code run's
    // preflight stops on the refusal.
    private void LogRefused(string refused, string? language) =>
        logger?.LogWarning(
            "p0265: context.yaml stack.image '{Image}' is outside the trusted registries "
            + "[{Trusted}], so the sandbox for lang={Lang} is not started in it.",
            refused, _trust.InForce().Description, language ?? "null");

    // 2026-09-24-7e4b: the last link is what is left when no image was named — git and no
    // language toolchain at all, so it says so instead of surfacing as a command-not-found.
    private void LogFellThrough(string? language) =>
        logger?.LogWarning(
            "No toolchain image is known for lang={Lang}, so this sandbox gets '{Image}' — "
            + "git and no language toolchain, so nothing can be built, tested or version-checked "
            + "in it. Name the image in the context's stack.image, or in sandbox.images for that "
            + "language, or in sandbox.toolchain_image for the whole project. Known languages: "
            + "{Known}.",
            language ?? "null", GenericFallbackImage,
            string.Join(", ", ToolchainImageCatalog.KnownLanguages.Keys));

    // p0245: the operator's per-language image override, matched case-insensitively
    // like the code table. An empty value or missing key falls through (null).
    private static string? ConfiguredImage(ResolvedProject projectConfig, string? language)
    {
        var images = projectConfig.Sandbox?.Images;
        if (images is null || string.IsNullOrEmpty(language)) return null;
        return images.FirstOrDefault(kv =>
            string.Equals(kv.Key, language, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(kv.Value)).Value;
    }
}
