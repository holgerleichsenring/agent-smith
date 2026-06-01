using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;

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
    IAgentImageResolver agentImageResolver)
{
    // Keys cover both ProjectMap.PrimaryLanguage's analyzer output (lowercase
    // canonical: csharp / node / typescript / python / go / rust) AND the
    // operator-facing strings the context.yaml schema documents under stack.lang
    // (C#, .NET 8, TypeScript, JavaScript, Python, Go, Rust). The dictionary is
    // OrdinalIgnoreCase so case variants resolve too. Adding a new language
    // means a row here plus its image — no glue code on call sites.
    private static readonly Dictionary<string, string> LanguageImages = new(StringComparer.OrdinalIgnoreCase)
    {
        // .NET / C# family — canonical + operator-facing variants
        ["dotnet8"] = "mcr.microsoft.com/dotnet/sdk:8.0",
        ["dotnet9"] = "mcr.microsoft.com/dotnet/sdk:9.0",
        ["dotnet"] = "mcr.microsoft.com/dotnet/sdk:8.0",
        [".net"] = "mcr.microsoft.com/dotnet/sdk:8.0",
        [".net 8"] = "mcr.microsoft.com/dotnet/sdk:8.0",
        [".net 9"] = "mcr.microsoft.com/dotnet/sdk:9.0",
        ["csharp"] = "mcr.microsoft.com/dotnet/sdk:8.0",
        ["c#"] = "mcr.microsoft.com/dotnet/sdk:8.0",
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

    public SandboxSpec Build(ResolvedProject projectConfig, ProjectMap? projectMap)
        => Build(projectConfig, projectMap?.PrimaryLanguage);

    public SandboxSpec Build(ResolvedProject projectConfig, string? language)
    {
        var image = ResolveImage(projectConfig, language);
        var resources = resourceResolver.Resolve(projectConfig);
        var agentImage = agentImageResolver.Resolve(projectConfig);
        return new SandboxSpec(ToolchainImage: image, Resources: resources, AgentImage: agentImage);
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

    private static string ResolveImage(ResolvedProject projectConfig, string? language)
    {
        var override_ = projectConfig.Sandbox?.ToolchainImage;
        if (!string.IsNullOrEmpty(override_)) return override_;

        if (!string.IsNullOrEmpty(language) && LanguageImages.TryGetValue(language, out var image))
            return image;

        return GenericFallbackImage;
    }
}
