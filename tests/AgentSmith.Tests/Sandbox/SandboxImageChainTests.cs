using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-08-31-77a8: the resolution order after the domain profile's tier was removed —
/// project override, per-language operator table, the context's own declared image, the
/// language convention table, the generic fallback. Nothing between the declared image
/// and the table any more, and nothing that refuses a run before a sandbox exists.
/// </summary>
public sealed class SandboxImageChainTests
{
    private readonly SandboxImageChain _sut = new();

    [Fact]
    public void ImageChain_ResolvesWithoutAProfileTier()
    {
        // The tier that used to sit here brought an image the repository never named,
        // and refused the run outright when that image failed the registry gate.
        _sut.Resolve(new ResolvedProject(), "go", contextImage: null)
            .Should().Be("golang:1.22-bookworm", "the language table is what follows a declared image");

        _sut.Resolve(new ResolvedProject(), "python", "node:20-bookworm")
            .Should().Be("node:20-bookworm", "a declared image is the image that gets used");

        _sut.Resolve(new ResolvedProject(), language: null, contextImage: null)
            .Should().Be(SandboxImageChain.GenericFallbackImage,
                "an unknown language still resolves — it never refuses");
    }

    [Fact]
    public void ResolveImage_PerLanguageOperatorTable_WinsOverTheLanguageTable()
    {
        var project = new ResolvedProject
        {
            Sandbox = new SandboxConfig
            {
                Images = new Dictionary<string, string> { ["go"] = "golang:1.22-bookworm" },
            },
        };

        _sut.Resolve(project, "go", contextImage: null).Should().Be("golang:1.22-bookworm");
    }

    [Fact]
    public void ResolveImage_ProjectToolchainOverride_StillWinsOutright()
    {
        var project = new ResolvedProject
        {
            Sandbox = new SandboxConfig { ToolchainImage = "buildpack-deps:bookworm-scm" },
        };

        _sut.Resolve(project, "go", contextImage: "node:20-bookworm")
            .Should().Be("buildpack-deps:bookworm-scm");
    }

    [Fact]
    public void ResolveImage_AnUntrustedContextImage_FallsBackRatherThanRefusing() =>
        _sut.Resolve(new ResolvedProject(), "go", "example.invalid/tools/data:1-bookworm")
            .Should().Be("golang:1.22-bookworm",
                "the only refusal in the chain left with the profile tier");
}
