using AgentSmith.Application.Models.Sandbox;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-10-12-6bfab: entries and images are matched in the form Docker resolves them to, so
/// an operator who writes either form of a Docker Hub namespace trusts the same images.
/// </summary>
public sealed class RegistryTrustBoundaryTests
{
    private static RegistryTrustBoundary With(params string[] registries) =>
        RegistryTrustBoundary.From(new SandboxGlobalConfig { AllowedRegistries = [.. registries] });

    [Theory]
    [InlineData("docker.io/sbtscala/")]
    [InlineData("sbtscala/")]
    [InlineData("sbtscala")]
    [InlineData("docker.io/sbtscala")]
    public void From_SbtscalaWithAndWithoutDockerIo_MatchEitherImageForm(string entry)
    {
        var boundary = With(entry);

        boundary.Accepts("sbtscala/scala-sbt:eclipse-temurin-17.0.4_1.7.1_3.2.0").Should().BeTrue();
        boundary.Accepts("docker.io/sbtscala/scala-sbt:1").Should().BeTrue();
        boundary.Registries.Should().Equal("docker.io/sbtscala/");
    }

    [Fact]
    public void Accepts_NamespaceEntry_DoesNotMatchALongerNamespace() =>
        With("docker.io/sbtscala/").Accepts("sbtscala-evil/scala-sbt:1").Should().BeFalse();

    [Theory]
    [InlineData("node:20-bookworm")]
    [InlineData("docker.io/node:20-bookworm")]
    [InlineData("docker.io/library/node:20-bookworm")]
    [InlineData("mcr.microsoft.com:latest")]
    public void Accepts_DockerIoLibraryForms_FollowTheLibrarySwitch(string image)
    {
        RegistryTrustBoundary.From(new SandboxGlobalConfig()).Accepts(image).Should().BeTrue();
        RegistryTrustBoundary.From(new SandboxGlobalConfig { AllowDockerHubLibrary = false })
            .Accepts(image).Should().BeFalse();
    }

    [Fact]
    public void Accepts_BareDockerIoEntry_TrustsAllOfDockerHub()
    {
        var boundary = With("docker.io");

        boundary.Accepts("someuser/tool:1").Should().BeTrue();
        boundary.Accepts("node:20-bookworm").Should().BeTrue(
            "a library image lives on docker.io too — the operator trusted the whole host");
        boundary.Accepts("ghcr.io/org/tool:1").Should().BeFalse();
    }

    [Fact]
    public void From_ReportsWhereEachHalfCameFrom()
    {
        var unset = RegistryTrustBoundary.From(new SandboxGlobalConfig());
        unset.Should().Be(new RegistryTrustBoundary(
            RegistryTrustBoundary.DefaultRegistries, true, true, false));

        var named = With("ghcr.io/org");
        named.RegistriesAreDefault.Should().BeFalse();
        named.DockerHubLibrary.Should().BeFalse("naming a registry narrows the library away");
        named.DockerHubLibraryIsExplicit.Should().BeFalse();
    }

    [Fact]
    public void Description_ClosedBoundary_SaysItCouldNotBeRead() =>
        RegistryTrustBoundary.Closed.Description.Should().Contain("could not be read");

    [Fact]
    public void Resolve_TrustedViaDockerIoEntry_ReturnsTheTrimmedImageAsWritten()
    {
        // The canonical form is for matching only — the sandbox runs the image as declared.
        var trust = new Mock<IImageRegistryTrust>();
        trust.Setup(t => t.InForce()).Returns(With("docker.io/sbtscala/"));

        new SandboxImageChain(trust.Object)
            .Resolve(new ResolvedProject(), "scala", "  sbtscala/scala-sbt:1 ")
            .Should().Be("sbtscala/scala-sbt:1");
    }
}
