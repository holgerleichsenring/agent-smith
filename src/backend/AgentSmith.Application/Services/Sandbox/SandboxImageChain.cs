using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
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
///   4. the domain profile's image (p0504) — reached only when the context
///      declared none, and REFUSED rather than dropped when it fails the gate
///   5. the language convention table
///   6. the generic git-bearing fallback
/// </para>
/// </summary>
public sealed class SandboxImageChain(ILogger<SandboxImageChain>? logger = null)
{
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
        ResolvedProject projectConfig, string? language, string? contextImage, string? profileImage = null)
    {
        ArgumentNullException.ThrowIfNull(projectConfig);
        var projectOverride = projectConfig.Sandbox?.ToolchainImage;
        if (!string.IsNullOrEmpty(projectOverride)) return projectOverride;
        if (ConfiguredImage(projectConfig, language) is { } configured) return configured;
        if (AcceptedContextImage(contextImage, language) is { } accepted) return accepted;
        if (GatedProfileImage(profileImage) is { } fromProfile) return fromProfile;
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

    // p0265: validate an LLM-named stack.image before trusting it as the sandbox
    // toolchain. Returns the image when it clears both gates, else null (the chain
    // continues) with a WARN so the rejection is visible.
    private string? AcceptedContextImage(string? contextImage, string? language)
    {
        var trimmed = contextImage?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (!ToolchainImageCatalog.IsTrustedRegistry(trimmed))
        {
            logger?.LogWarning(
                "p0265: context.yaml stack.image '{Image}' is not from a trusted registry "
                + "(mcr.microsoft.com, ghcr.io, or a Docker Hub official library image). "
                + "Falling back for lang={Lang}.", trimmed, language ?? "null");
            return null;
        }
        if (!ToolchainImageCatalog.IsGitBearing(trimmed))
        {
            logger?.LogWarning(
                "p0265: context.yaml stack.image '{Image}' does not match a git-bearing tag "
                + "(a sandbox runs `git clone` inside it; -slim/-alpine/bare tags lack git). "
                + "Falling back for lang={Lang}.", trimmed, language ?? "null");
            return null;
        }
        logger?.LogInformation(
            "p0265: using LLM-named context.yaml stack.image '{Image}' (lang={Lang}).",
            trimmed, language ?? "null");
        return trimmed;
    }

    // p0504: a profile image that fails the gate REFUSES. Falling through to the
    // language table would run the profile's commands in an image that never
    // carried them — the failure this profile mechanism exists to prevent.
    private static string? GatedProfileImage(string? profileImage)
    {
        var trimmed = profileImage?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (!ToolchainImageCatalog.IsTrustedRegistry(trimmed))
            throw new ConfigurationException(
                $"Domain profile image '{trimmed}' is not from a trusted registry "
                + "(mcr.microsoft.com, ghcr.io, or a Docker Hub official library image). "
                + "Fix the profile in the skills catalog; no sandbox is started for it.");
        if (!ToolchainImageCatalog.IsGitBearing(trimmed))
            throw new ConfigurationException(
                $"Domain profile image '{trimmed}' does not match a git-bearing tag — a sandbox "
                + "runs `git clone` inside it. Fix the profile in the skills catalog; no sandbox "
                + "is started for it.");
        return trimmed;
    }
}
