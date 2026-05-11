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
    // exec's `/shared/agent` (the sandbox-agent self-contained .NET binary). The
    // agent is built on dotnet/runtime-deps:8.0-bookworm — glibc-linked — so the
    // toolchain must also be glibc-based. alpine:3.20 was tried (~8 MB, has git
    // out of the box) and rejected: its musl libc lacks the glibc dynamic linker
    // referenced in the agent's ELF header, so `exec /shared/agent` returns
    // ENOENT — observable as a confusing "no such file or directory" even though
    // ls shows the binary in place. debian:bookworm-slim is glibc + ~80 MB; git
    // is not preinstalled there, so we use debian:bookworm (~124 MB) which ships
    // both. Operators with stricter base-image policies override via
    // ProjectConfig.Sandbox.ToolchainImage.
    private const string GenericFallbackImage = "debian:bookworm";

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
