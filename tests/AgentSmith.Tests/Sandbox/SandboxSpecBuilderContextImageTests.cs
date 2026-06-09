using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0265: the analyzer/context-generator LLM names the exact toolchain image in
/// context.yaml `stack.image`. It must win over the language→image convention
/// table (so a net8 repo gets sdk:8.0 that can RUN its tests, and frameworks
/// with no table row still get a working image) — but only after clearing the
/// supply-chain + git-bearing gate. An invalid image falls back to the table.
/// Operator override (Sandbox.ToolchainImage) still wins over everything.
/// </summary>
public sealed class SandboxSpecBuilderContextImageTests
{
    private static SandboxSpecBuilder NewSut() =>
        new(new StubSandboxResourceResolver(), new StubAgentImageResolver());

    [Fact]
    public void ContextImage_WinsOverLanguageTable()
    {
        // lang "csharp" → table sdk:9.0, but the LLM named sdk:8.0 for a net8 repo.
        var spec = NewSut().Build(
            new ResolvedProject(), language: "csharp",
            contextImage: "mcr.microsoft.com/dotnet/sdk:8.0");

        spec.ToolchainImage.Should().Be("mcr.microsoft.com/dotnet/sdk:8.0");
    }

    [Fact]
    public void ContextImage_FrameworkWithNoTableRow_StillResolves()
    {
        // Angular et al. have no table row; the LLM-named git-bearing node image wins.
        var spec = NewSut().Build(
            new ResolvedProject(), language: "angular",
            contextImage: "node:20-bookworm");

        spec.ToolchainImage.Should().Be("node:20-bookworm");
    }

    [Theory]
    [InlineData("evil.example.com/pwn:latest")]   // untrusted registry
    [InlineData("someuser/dotnet-sdk:8.0")]       // not an official library image
    [InlineData("node:20-alpine")]                // trusted but gitless
    [InlineData("mcr.microsoft.com/dotnet/sdk")]  // no tag → not git-bearing pattern
    public void InvalidContextImage_FallsBackToLanguageTable(string contextImage)
    {
        var spec = NewSut().Build(
            new ResolvedProject(), language: "csharp", contextImage: contextImage);

        // csharp table entry, not the rejected LLM image.
        spec.ToolchainImage.Should().Be("mcr.microsoft.com/dotnet/sdk:9.0");
    }

    [Fact]
    public void OperatorOverride_WinsOverContextImage()
    {
        var project = new ResolvedProject
        {
            Sandbox = new SandboxConfig { ToolchainImage = "my-mirror/dotnet:8.0" },
        };

        var spec = NewSut().Build(
            project, language: "csharp", contextImage: "mcr.microsoft.com/dotnet/sdk:8.0");

        spec.ToolchainImage.Should().Be("my-mirror/dotnet:8.0");
    }
}
