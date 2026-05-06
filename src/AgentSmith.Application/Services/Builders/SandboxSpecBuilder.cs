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
    // security-scan, mad-discussion, legal-analysis). InProcessSandboxFactory ignores
    // the image entirely; Docker/K8s factories use it as the toolchain container — alpine
    // gives them a working shell + git + coreutils so the sandbox-side `git clone` Step
    // and SandboxFileReader file IO work without a heavier language SDK on board.
    // Operators with stricter base-image policies override via ProjectConfig.Sandbox.ToolchainImage.
    private const string GenericFallbackImage = "alpine:3.20";

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
