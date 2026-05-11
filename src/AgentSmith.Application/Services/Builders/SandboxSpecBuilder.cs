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
    // a misleading ENOENT) AND git on PATH (used by CheckoutSource). Built locally
    // from src/AgentSmith.ToolchainGeneric/Dockerfile (debian-slim + git, ~130 MB).
    // Operators with stricter base-image policies override via
    // ProjectConfig.Sandbox.ToolchainImage.
    private const string GenericFallbackImage = "agent-smith-toolchain-generic:latest";

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
