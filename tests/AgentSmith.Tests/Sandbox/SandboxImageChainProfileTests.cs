using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0504: where the domain profile's image sits in the resolution order. It is the last
/// operator-shaped layer before convention — below every declared image, above the
/// language table — and it REFUSES rather than falling back when it fails the gate.
/// </summary>
public sealed class SandboxImageChainProfileTests
{
    private const string ProfileImage = "python:3.12-bookworm";

    private readonly SandboxImageChain _sut = new();

    [Fact]
    public void ResolveImage_StackImageAndProfileBothPresent_TheStackImageWins() =>
        _sut.Resolve(new ResolvedProject(), "python", "node:20-bookworm", ProfileImage)
            .Should().Be("node:20-bookworm");

    [Fact]
    public void ResolveImage_ProfileSetAndNoStackImage_BeatsTheLanguageTable() =>
        _sut.Resolve(new ResolvedProject(), "go", contextImage: null, profileImage: ProfileImage)
            .Should().Be(ProfileImage);

    [Fact]
    public void ResolveImage_PerLanguageOperatorTable_StillWinsOverTheProfile()
    {
        var project = new ResolvedProject
        {
            Sandbox = new SandboxConfig
            {
                Images = new Dictionary<string, string> { ["go"] = "golang:1.22-bookworm" },
            },
        };

        _sut.Resolve(project, "go", contextImage: null, profileImage: ProfileImage)
            .Should().Be("golang:1.22-bookworm");
    }

    [Fact]
    public void ResolveImage_ProjectToolchainOverride_StillWinsOutright()
    {
        var project = new ResolvedProject
        {
            Sandbox = new SandboxConfig { ToolchainImage = "buildpack-deps:bookworm-scm" },
        };

        _sut.Resolve(project, "go", contextImage: "node:20-bookworm", profileImage: ProfileImage)
            .Should().Be("buildpack-deps:bookworm-scm");
    }

    [Fact]
    public void Profile_ImageFromAnUntrustedRegistry_RefusesRatherThanFallingBack()
    {
        var act = () => _sut.Resolve(
            new ResolvedProject(), "python", contextImage: null,
            profileImage: "example.invalid/tools/data:1-bookworm");

        act.Should().Throw<ConfigurationException>().WithMessage("*trusted registries*");
    }

    [Fact]
    public void Profile_ImageWithASlimTag_IsUsedRatherThanRefusedOnItsName()
    {
        // 2026-08-25-014d: a profile brings its own image BECAUSE the profile's commands
        // live in it. Refusing it over a tag shape decided, from the name alone, that the
        // catalog was wrong about its own toolchain — and the run never started. If it
        // really carries no git, the checkout says so, in that image.
        _sut.Resolve(new ResolvedProject(), "python", contextImage: null, profileImage: "python:3.12-slim")
            .Should().Be("python:3.12-slim");
    }

    [Fact]
    public void ResolveImage_NoProfileAndNoDeclaredImage_StillUsesTheLanguageTable() =>
        _sut.Resolve(new ResolvedProject(), "go", contextImage: null, profileImage: null)
            .Should().Be("golang:1.22-bookworm");
}
