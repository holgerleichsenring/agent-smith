using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0504: picks the one toolchain image a sandbox runs in. Extracted from
/// SandboxSpecBuilder so the ordering — which is operator policy, not building —
/// lives in a type that can be read and tested on its own.
/// <para>
/// Order, most authoritative first:
///   1. sandbox.toolchain_image (operator, whole project) — wins outright
///   2. sandbox.images[lang] (operator, per language, p0245)
///   3. context.yaml stack.image (LLM-named, p0265) — a declared image is the
///      image that gets used
///   4. the language convention table
///   5. the generic git-bearing fallback
/// </para>
/// <para>
/// 2026-08-25-014d: the only thing an image is judged by here is where it comes
/// from. What it CONTAINS is discovered where it is used — a sandbox clones into
/// itself, so an image without git fails at the checkout, by name. The tag
/// pattern that used to guess at git could only downgrade to a different image,
/// silently, and guessed wrong for every ecosystem it had never heard of.
/// </para>
/// </summary>
public sealed class SandboxImageChain(
    ImageRegistryTrust? trust = null, ILogger<SandboxImageChain>? logger = null)
{
    private readonly ImageRegistryTrust _trust = trust ?? new ImageRegistryTrust();

    // Generic fallback when no language-specific image can be resolved.
    //
    // Requirements: glibc (the self-contained .NET 8 agent binary is glibc-linked
    // via its carrier dotnet/runtime-deps base — musl toolchains crash exec with
    // a misleading ENOENT) AND git on PATH (used by CheckoutSource).
    //
    // buildpack-deps:bookworm-scm is the Docker-official SCM toolbox image:
    // Debian bookworm (glibc 2.36) + git + ca-certs + openssl + curl + wget.
    // Public Docker Hub image — works on k8s without operator-side build steps.
    // Operators with stricter base-image policies override via
    // ResolvedProject.Sandbox.ToolchainImage.
    public const string GenericFallbackImage = "buildpack-deps:bookworm-scm";

    public string Resolve(
        ResolvedProject projectConfig, string? language, string? contextImage)
    {
        ArgumentNullException.ThrowIfNull(projectConfig);
        var projectOverride = projectConfig.Sandbox?.ToolchainImage;
        if (!string.IsNullOrEmpty(projectOverride)) return projectOverride;
        if (ConfiguredImage(projectConfig, language) is { } configured) return configured;
        if (AcceptedContextImage(contextImage, language) is { } accepted) return accepted;
        return ToolchainImageCatalog.ForLanguage(language) ?? GenericFallbackImage;
    }

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

    // p0265: judge an LLM-named stack.image against the operator's registry policy
    // before trusting it as the sandbox toolchain. Returns the image when it is
    // inside the boundary, else null (the chain continues) with a WARN.
    private string? AcceptedContextImage(string? contextImage, string? language)
    {
        var trimmed = contextImage?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (!_trust.Accepts(trimmed))
        {
            logger?.LogWarning(
                "p0265: context.yaml stack.image '{Image}' is outside the trusted registries "
                + "[{Trusted}]. Falling back for lang={Lang}.",
                trimmed, _trust.Description, language ?? "null");
            return null;
        }
        logger?.LogInformation(
            "p0265: using LLM-named context.yaml stack.image '{Image}' (lang={Lang}).",
            trimmed, language ?? "null");
        return trimmed;
    }

}
