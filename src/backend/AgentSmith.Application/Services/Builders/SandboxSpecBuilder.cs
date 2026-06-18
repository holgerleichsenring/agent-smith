using System.Text.RegularExpressions;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Builders;

/// <summary>
/// Resolves a SandboxSpec from ResolvedProject + ProjectMap. Convention-driven:
/// language → toolchain image. Per-project overrides via SandboxConfig win.
/// Resources resolved through <see cref="ISandboxResourceResolver"/> so the
/// resulting <see cref="SandboxSpec.Resources"/> always reflects the operator's
/// per-project override or the global <c>Sandbox</c> options defaults.
/// </summary>
public sealed class SandboxSpecBuilder(
    ISandboxResourceResolver resourceResolver,
    IAgentImageResolver agentImageResolver,
    // p0230: optional so the many test construction sites keep compiling; when
    // absent the step cap resolves against fresh SandboxGlobalConfig defaults.
    Microsoft.Extensions.Options.IOptions<SandboxGlobalConfig>? globalConfig = null,
    // p0265: optional so existing test construction sites keep compiling; used
    // only to log when an LLM-named context.yaml stack.image is rejected and we
    // fall back to the language table (never a silent defer).
    ILogger<SandboxSpecBuilder>? logger = null,
    // p0270a: the single config resolver provides the effective step timeout
    // (override ?? global) with provenance. Optional so the many bare test
    // construction sites keep compiling; when absent the step cap falls back to
    // the same inline arithmetic the deleted SandboxGlobalConfig.ResolveStepTimeout used.
    Configuration.IConfigResolver? configResolver = null,
    // p0272: parses the operator's sandbox.secrets block onto the spec. Optional
    // so the bare test construction sites keep compiling; the resolver is pure
    // (no deps), so the inline default matches the DI-registered instance.
    Sandbox.ISandboxSecretsResolver? secretsResolver = null)
{
    private readonly SandboxGlobalConfig _global = globalConfig?.Value ?? new SandboxGlobalConfig();
    private readonly Sandbox.ISandboxSecretsResolver _secretsResolver =
        secretsResolver ?? new Sandbox.SandboxSecretsResolver();
    // Keys cover both ProjectMap.PrimaryLanguage's analyzer output (lowercase
    // canonical: csharp / node / typescript / python / go / rust) AND the
    // operator-facing strings the context.yaml schema documents under stack.lang
    // (C#, .NET 8, TypeScript, JavaScript, Python, Go, Rust). The dictionary is
    // OrdinalIgnoreCase so case variants resolve too. Adding a new language
    // means a row here plus its image — no glue code on call sites.
    private static readonly Dictionary<string, string> LanguageImages = new(StringComparer.OrdinalIgnoreCase)
    {
        // .NET / C# family — canonical + operator-facing variants.
        // Bare C#/.NET resolve to the LATEST SDK: the .NET 9 SDK builds every
        // supported TFM (net8.0, net9.0, …), so it is the strictly-safer default
        // for a "C#" project of unknown/mixed target (a solution can mix net8 +
        // net9, as real estates do). Explicit dotnet8/.net 8 still pin 8.0.
        ["dotnet8"] = "mcr.microsoft.com/dotnet/sdk:8.0",
        ["dotnet9"] = "mcr.microsoft.com/dotnet/sdk:9.0",
        ["dotnet"] = "mcr.microsoft.com/dotnet/sdk:9.0",
        [".net"] = "mcr.microsoft.com/dotnet/sdk:9.0",
        [".net 8"] = "mcr.microsoft.com/dotnet/sdk:8.0",
        [".net 9"] = "mcr.microsoft.com/dotnet/sdk:9.0",
        ["csharp"] = "mcr.microsoft.com/dotnet/sdk:9.0",
        ["c#"] = "mcr.microsoft.com/dotnet/sdk:9.0",
        // Node / TS / JS — full bookworm (not -slim) because git must be
        // present in the sandbox: CheckoutSourceHandler runs `git clone`
        // INSIDE the sandbox, and the -slim variants drop git to save ~750MB.
        ["node"] = "node:20-bookworm",
        ["node20"] = "node:20-bookworm",
        ["node.js"] = "node:20-bookworm",
        ["nodejs"] = "node:20-bookworm",
        ["javascript"] = "node:20-bookworm",
        ["typescript"] = "node:20-bookworm",
        // Python — same reason, drop -slim so git is in the image.
        ["python"] = "python:3.12-bookworm",
        ["python3"] = "python:3.12-bookworm",
        // Go
        ["go"] = "golang:1.22-bookworm",
        ["golang"] = "golang:1.22-bookworm",
        // Rust
        ["rust"] = "rust:1.79-bookworm"
    };

    /// <summary>p0194: tests read this to pin every entry against a
    /// git-bearing image allowlist. CheckoutSourceHandler clones inside the
    /// sandbox, so a slim/alpine entry would break checkout silently.</summary>
    internal static IReadOnlyDictionary<string, string> KnownLanguages => LanguageImages;

    // p0265: a sandbox runs `git clone` inside the toolchain image
    // (CheckoutSourceHandler), so the image MUST bundle git. These patterns
    // recognise git-bearing tags; a -slim / -alpine / bare tag matches none and
    // is rejected. Single source of truth — both the LanguageImages allowlist
    // test (p0194) and the LLM-named stack.image validation use it.
    internal static readonly Regex[] GitBearingImagePatterns =
    [
        // Microsoft .NET SDK images include git in every tag.
        new(@"^mcr\.microsoft\.com/dotnet/sdk:", RegexOptions.Compiled),
        // Debian bookworm full base bundles git.
        new(@":[^-]*-bookworm$", RegexOptions.Compiled),
        // Debian bullseye full base bundles git.
        new(@":[^-]*-bullseye$", RegexOptions.Compiled),
        // The -scm suffix on buildpack-deps is explicitly source-control-tooling.
        new(@"^buildpack-deps:[^-]+-scm$", RegexOptions.Compiled),
    ];

    // p0265: trusted registries an LLM-named stack.image may pull from. A
    // supply-chain boundary (feedback_safety_in_api_not_process): the image
    // string is LLM-authored, so we only accept official Microsoft, GitHub
    // Container Registry, or Docker Hub *official library* images (single repo
    // segment, no user namespace). Anything else falls back to the language table.
    internal static bool IsTrustedRegistry(string image)
    {
        if (image.StartsWith("mcr.microsoft.com/", StringComparison.Ordinal)) return true;
        if (image.StartsWith("ghcr.io/", StringComparison.Ordinal)) return true;
        // Docker Hub official "library" image: the repository part (before the
        // tag) has no '/', e.g. node:20-bookworm, buildpack-deps:bookworm-scm.
        // user/repo or other-registry.tld/... both contain a '/' and are rejected.
        var repo = image.Split(':', 2)[0];
        return !repo.Contains('/', StringComparison.Ordinal);
    }

    public SandboxSpec Build(ResolvedProject projectConfig, ProjectMap? projectMap)
        => Build(projectConfig, projectMap?.PrimaryLanguage);

    public SandboxSpec Build(
        ResolvedProject projectConfig, string? language, string? contextImage = null,
        ContextYamlStackResources? contextResources = null)
    {
        var image = ResolveImage(projectConfig, language, contextImage);
        // p0268: context.yaml stack.resources sizes the sandbox as a layer between the
        // operator project override and the global default (validated in the resolver).
        var resources = resourceResolver.Resolve(projectConfig, contextResources);
        var agentImage = agentImageResolver.Resolve(projectConfig);
        // p0230/p0270a: the per-step wall-time cap (project override ?? global) now
        // comes from the single ConfigResolver so the spec carries exactly what the
        // dashboard shows. The inline fallback covers bare test construction sites
        // that don't inject a resolver — identical to the retired ResolveStepTimeout.
        var stepTimeout = configResolver?.ResolveStepTimeout(projectConfig).Value
            ?? (projectConfig.Sandbox?.StepTimeoutSeconds ?? _global.StepTimeoutSeconds);
        // p0272: parse the operator's sandbox.secrets onto the spec (fail-fast on a
        // malformed reference); PodSpecBuilder turns these into secretKeyRef env +
        // Secret-volume mounts. Null/absent block resolves to ResolvedSandboxSecrets.Empty.
        var secrets = _secretsResolver.Resolve(projectConfig.Sandbox);
        return new SandboxSpec(
            ToolchainImage: image, Resources: resources, AgentImage: agentImage,
            StepTimeoutSeconds: stepTimeout, Secrets: secrets);
    }

    // Generic fallback when no language-specific image can be resolved.
    //
    // Resolution chain (p0135) — the call site (PipelineExecutor.TryCreateSandboxAsync)
    // walks these in order via SandboxLanguageResolver:
    //   1. ResolvedProject.Sandbox.ToolchainImage (operator override) — wins outright
    //   2. SandboxLanguageResolver.TryResolveFromCacheAsync → host-side project-map.json
    //   3. SandboxLanguageResolver.TryResolveFromContextYamlAsync → remote
    //      .agentsmith/context.yaml via ISourceProvider.TryReadFileAsync
    //   4. ContextKeys.ProjectMap if already in-memory (never today, kept for symmetry)
    //   5. This fallback — for true unknowns and for scan / discussion pipelines
    //      that legitimately don't need a language SDK.
    //
    // Requirements: glibc (the self-contained .NET 8 agent binary is glibc-linked
    // via its carrier dotnet/runtime-deps base — musl toolchains crash exec with
    // a misleading ENOENT) AND git on PATH (used by CheckoutSource).
    //
    // buildpack-deps:bookworm-scm is the Docker-official SCM toolbox image:
    // Debian bookworm (glibc 2.36) + git + ca-certs + openssl + curl + wget +
    // hg + svn. ~371 MB pulled once per node. Public Docker Hub image — works
    // on k8s without operator-side build steps. Earlier attempts: alpine:3.20
    // (rejected: musl libc, agent crashes with ENOENT), debian:bookworm
    // (rejected: no git out of the box), self-built debian-slim+git carrier
    // (rejected: needs a CI/push pipeline that doesn't exist yet).
    //
    // Operators with stricter base-image policies override via
    // ResolvedProject.Sandbox.ToolchainImage.
    private const string GenericFallbackImage = "buildpack-deps:bookworm-scm";

    private string ResolveImage(ResolvedProject projectConfig, string? language, string? contextImage)
    {
        // 1. Operator override (agentsmith.yml sandbox.toolchain_image) — wins outright.
        var override_ = projectConfig.Sandbox?.ToolchainImage;
        if (!string.IsNullOrEmpty(override_)) return override_;

        // 2. p0265: LLM-named context.yaml stack.image — wins over the convention
        //    table when it passes the supply-chain + git-bearing gate. This is how
        //    a net8 repo gets sdk:8.0 (runs its tests) and how frameworks with no
        //    table row (Angular, …) get a working image without per-language glue.
        if (TryAcceptContextImage(contextImage, language) is { } accepted) return accepted;

        // 3. Language convention table.
        if (!string.IsNullOrEmpty(language) && LanguageImages.TryGetValue(language, out var image))
            return image;

        // 4. Generic git-bearing fallback.
        return GenericFallbackImage;
    }

    // p0265: validate an LLM-named stack.image before trusting it as the sandbox
    // toolchain. Returns the image when it clears both gates, else null (caller
    // falls back to the language table) with a WARN so the rejection is visible.
    private string? TryAcceptContextImage(string? contextImage, string? language)
    {
        var trimmed = contextImage?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;

        if (!IsTrustedRegistry(trimmed))
        {
            logger?.LogWarning(
                "p0265: context.yaml stack.image '{Image}' is not from a trusted registry "
                + "(mcr.microsoft.com, ghcr.io, or a Docker Hub official library image). "
                + "Falling back to the language table for lang={Lang}.",
                trimmed, language ?? "null");
            return null;
        }

        if (!GitBearingImagePatterns.Any(p => p.IsMatch(trimmed)))
        {
            logger?.LogWarning(
                "p0265: context.yaml stack.image '{Image}' does not match a git-bearing tag "
                + "(a sandbox runs `git clone` inside it; -slim/-alpine/bare tags lack git). "
                + "Falling back to the language table for lang={Lang}.",
                trimmed, language ?? "null");
            return null;
        }

        logger?.LogInformation(
            "p0265: using LLM-named context.yaml stack.image '{Image}' (lang={Lang}).",
            trimmed, language ?? "null");
        return trimmed;
    }
}
