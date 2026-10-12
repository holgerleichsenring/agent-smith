using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-10-12-6bfac: a stack.image the registry boundary refuses is named with the boundary
/// and the exact lever, instead of a server-log line nobody reads.
/// </summary>
public sealed class ContextImageBoundaryTests
{
    private const string UserNamespaceImage = "sbtscala/scala-sbt:eclipse-temurin-25_1.11.7_2.13.18";

    private static ContextImageBoundary Boundary(SandboxGlobalConfig? config = null) =>
        new(new ImageRegistryTrust(Options.Create(config ?? new SandboxGlobalConfig())));

    [Fact]
    public void Judge_UserNamespaceImage_NamesImageBoundaryAndNamespacePrefix()
    {
        var refusal = Boundary().Judge(UserNamespaceImage);

        refusal.Should().NotBeNull();
        refusal!.Image.Should().Be(UserNamespaceImage);
        refusal.Reason.Should().Contain(UserNamespaceImage).And.Contain("mcr.microsoft.com/");
        refusal.Lever.Should().Contain("'sbtscala/'").And.Contain("sandbox.allowed_registries")
            .And.Contain("replaces the built-in defaults",
                "following the lever as written must not silently refuse every default image")
            .And.Contain("sandbox.images").And.Contain("sandbox.toolchain_image");
    }

    [Theory]
    [InlineData("mcr.microsoft.com/dotnet/sdk:8.0")]
    [InlineData("node:20-bookworm")]
    [InlineData("")]
    [InlineData(null)]
    public void Judge_TrustedImage_ReturnsNull(string? image) =>
        Boundary().Judge(image).Should().BeNull();

    [Fact]
    public void Judge_LibraryImageRefusedByANamedList_PointsAtTheLibrarySwitch()
    {
        var refusal = Boundary(new SandboxGlobalConfig { AllowedRegistries = ["mirror.example.com"] })
            .Judge("eclipse-temurin:21-jdk");

        refusal!.Lever.Should().Contain("sandbox.allow_docker_hub_library")
            .And.NotContain("allowed_registries",
                "a namespace-free image has no prefix a registry entry could name");
    }

    [Fact]
    public void Judge_HostQualifiedImage_SuggestsPrefixThroughLastSlash()
    {
        Boundary().Judge("registry.example.com:5000/team/sbt:1.11@sha256:abc")!
            .Lever.Should().Contain("'registry.example.com:5000/team/'",
                "a registry port is not a tag, and a digest is not part of the repository");
        Boundary().Judge("quay.io/org/img:2")!.Lever.Should().Contain("'quay.io/org/'");
    }

    [Fact]
    public void Describe_JoinsReasonAndLever() =>
        new ContextImageRefusal("x/y:1", "the reason", "the lever").Describe()
            .Should().Be("the reason — the lever");
}
