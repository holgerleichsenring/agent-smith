using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0245: an operator can pin a toolchain image per language in agentsmith.yml
/// (sandbox.images), merged over the code-default table per key. It beats the
/// code table AND the LLM-named context.image (operator authority), but the
/// whole-project toolchain_image still wins outright; an unmatched language falls
/// through to the table/fallback.
/// </summary>
public sealed class SandboxSpecBuilderConfiguredImageTests
{
    private static SandboxSpecBuilder NewSut() =>
        new(new StubSandboxResourceResolver(), new StubAgentImageResolver());

    private static ResolvedProject ProjectWithImages(params (string Lang, string Image)[] images) =>
        new()
        {
            Sandbox = new SandboxConfig
            {
                Images = images.ToDictionary(i => i.Lang, i => i.Image)
            }
        };

    [Fact]
    public void ConfiguredImage_OverridesCodeDefault()
    {
        var spec = NewSut().Build(ProjectWithImages(("csharp", "my-mirror/dotnet:9.0")), language: "csharp", pipelineName: "fix-bug");

        spec.ToolchainImage.Should().Be("my-mirror/dotnet:9.0");
    }

    [Fact]
    public void ConfiguredImage_BeatsContextImage()
    {
        // Operator authority over the LLM-named (and otherwise valid) context image.
        var spec = NewSut().Build(
            ProjectWithImages(("node", "my-mirror/node:20")), language: "node", pipelineName: "fix-bug",
            contextImage: "node:20-bookworm");

        spec.ToolchainImage.Should().Be("my-mirror/node:20");
    }

    [Fact]
    public void ToolchainImageOverride_BeatsConfiguredImage()
    {
        var project = ProjectWithImages(("csharp", "my-mirror/dotnet:9.0"));
        project.Sandbox!.ToolchainImage = "corp-mirror/all:latest";

        var spec = NewSut().Build(project, language: "csharp", pipelineName: "fix-bug");

        spec.ToolchainImage.Should().Be("corp-mirror/all:latest");
    }

    [Fact]
    public void ConfiguredImage_CaseInsensitiveLanguageKey()
    {
        var spec = NewSut().Build(ProjectWithImages(("DOTNET", "my-mirror/dotnet:9.0")), language: "dotnet", pipelineName: "fix-bug");

        spec.ToolchainImage.Should().Be("my-mirror/dotnet:9.0");
    }

    [Fact]
    public void UnmatchedLanguage_FallsThroughToCodeTable()
    {
        // Images map covers dotnet only; a node repo still gets the table image.
        var spec = NewSut().Build(ProjectWithImages(("dotnet", "my-mirror/dotnet:9.0")), language: "node", pipelineName: "fix-bug");

        spec.ToolchainImage.Should().Be("node:20-bookworm");
    }
}
