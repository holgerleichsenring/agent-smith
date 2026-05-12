using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>
/// Resolves a SandboxSpec from ProjectConfig + ProjectMap. Convention-driven:
/// language → toolchain image. Per-project overrides via SandboxConfig win.
/// </summary>
public sealed class SandboxSpecBuilder
{
    private static readonly Dictionary<string, string> LanguageImages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dotnet8"] = "mcr.microsoft.com/dotnet/sdk:8.0",
        ["dotnet9"] = "mcr.microsoft.com/dotnet/sdk:9.0",
        ["dotnet"] = "mcr.microsoft.com/dotnet/sdk:8.0",
        ["csharp"] = "mcr.microsoft.com/dotnet/sdk:8.0",
        ["node"] = "node:20-bookworm-slim",
        ["node20"] = "node:20-bookworm-slim",
        ["javascript"] = "node:20-bookworm-slim",
        ["typescript"] = "node:20-bookworm-slim",
        ["python"] = "python:3.12-slim",
        ["python3"] = "python:3.12-slim",
        ["go"] = "golang:1.22-bookworm",
        ["rust"] = "rust:1.79-bookworm"
    };

    public SandboxSpec Build(ProjectConfig projectConfig, ProjectMap? projectMap)
    {
        var image = ResolveImage(projectConfig, projectMap);
        return new SandboxSpec(ToolchainImage: image);
    }

    // Generic fallback for pipelines that don't compile/test project code (api-scan,
    // security-scan, mad-discussion, legal-analysis) AND for the init-project window
    // before AnalyzeCode populates ProjectMap. InProcessSandboxFactory ignores the
    // image entirely; Docker/K8s factories use it as the toolchain container that
    // exec's `/shared/agent` and runs git clone for CheckoutSource.
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
    // ProjectConfig.Sandbox.ToolchainImage.
    private const string GenericFallbackImage = "buildpack-deps:bookworm-scm";

    private static string ResolveImage(ProjectConfig projectConfig, ProjectMap? projectMap)
    {
        var override_ = projectConfig.Sandbox?.ToolchainImage;
        if (!string.IsNullOrEmpty(override_)) return override_;

        var language = projectMap?.PrimaryLanguage;
        if (!string.IsNullOrEmpty(language) && LanguageImages.TryGetValue(language, out var image))
            return image;

        return GenericFallbackImage;
    }
}
