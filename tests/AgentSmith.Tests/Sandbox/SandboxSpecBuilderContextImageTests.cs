using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0265: the analyzer/context-generator LLM names the exact toolchain image in
/// context.yaml `stack.image`. It must win over the language→image convention
/// table (so a net8 repo gets sdk:8.0 that can RUN its tests, and frameworks
/// with no table row still get a working image) — but only after clearing the
/// supply-chain gate. An image outside it falls back to the table.
/// Operator override (Sandbox.ToolchainImage) still wins over everything.
/// <para>
/// 2026-08-25-014d: the gate is the registry boundary and nothing else. An image
/// the boundary accepts is USED, whatever its tag looks like — the tag shapes the
/// gate also weighed knew four ecosystems and could only answer a fifth wrongly.
/// </para>
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
            new ResolvedProject(), language: "csharp", pipelineName: "fix-bug",
            contextImage: "mcr.microsoft.com/dotnet/sdk:8.0");

        spec.ToolchainImage.Should().Be("mcr.microsoft.com/dotnet/sdk:8.0");
    }

    [Fact]
    public void ContextImage_FrameworkWithNoTableRow_StillResolves()
    {
        // Angular et al. have no table row; the LLM-named git-bearing node image wins.
        var spec = NewSut().Build(
            new ResolvedProject(), language: "angular", pipelineName: "fix-bug",
            contextImage: "node:20-bookworm");

        spec.ToolchainImage.Should().Be("node:20-bookworm");
    }

    [Theory]
    [InlineData("evil.example.com/pwn:latest")]   // untrusted registry
    [InlineData("someuser/dotnet-sdk:8.0")]       // not an official library image
    [InlineData("a.host:5000/pwn:latest")]        // a port is not a tag, so this is no library image
    public void InvalidContextImage_FallsBackToLanguageTable(string contextImage)
    {
        var spec = NewSut().Build(
            new ResolvedProject(), language: "csharp", pipelineName: "fix-bug", contextImage: contextImage);

        // csharp table entry, not the rejected LLM image.
        spec.ToolchainImage.Should().Be("mcr.microsoft.com/dotnet/sdk:9.0");
    }

    [Theory]
    [InlineData("node:20-alpine")]                // inside the boundary; its contents are its own business
    [InlineData("mcr.microsoft.com/dotnet/sdk")]  // untagged, and still from Microsoft
    public void ContextImage_InsideTheBoundary_IsUsedWhateverItsTagLooksLike(string contextImage)
    {
        var spec = NewSut().Build(
            new ResolvedProject(), language: "csharp", pipelineName: "fix-bug", contextImage: contextImage);

        spec.ToolchainImage.Should().Be(contextImage);
    }

    [Fact]
    public void OperatorOverride_WinsOverContextImage()
    {
        var project = new ResolvedProject
        {
            Sandbox = new SandboxConfig { ToolchainImage = "my-mirror/dotnet:8.0" },
        };

        var spec = NewSut().Build(
            project, language: "csharp", pipelineName: "fix-bug", contextImage: "mcr.microsoft.com/dotnet/sdk:8.0");

        spec.ToolchainImage.Should().Be("my-mirror/dotnet:8.0");
    }
}
