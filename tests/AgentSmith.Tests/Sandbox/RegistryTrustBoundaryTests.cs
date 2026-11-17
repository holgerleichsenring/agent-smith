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
    public void Accepts_DockerIoLibraryForms_FollowTheLibraryEntry(string image)
    {
        RegistryTrustBoundary.From(new SandboxGlobalConfig()).Accepts(image).Should().BeTrue(
            "2026-11-16-f3fbd: docker.io/library/ is part of the built-in default");
        With("docker.io/library/").Accepts(image).Should().BeTrue();
        With("ghcr.io/").Accepts(image).Should().BeFalse("a named list without the entry leaves the library out");
    }

    [Fact]
    public void Accepts_LibraryEntry_TrustsNodeButNoUserNamespace()
    {
        var boundary = With("docker.io/library/");

        boundary.Accepts("node:22-bookworm").Should().BeTrue();
        boundary.Accepts("someuser/node:22-bookworm").Should().BeFalse();
    }

    [Theory]
    [InlineData("node:22-bookworm", "docker.io/library/")]
    [InlineData("docker.io/node:22", "docker.io/library/")]
    [InlineData("sbtscala/scala-sbt:1", "docker.io/sbtscala/")]
    [InlineData("registry.example.com:5000/team/sbt:1.11@sha256:abc", "registry.example.com:5000/team/")]
    [InlineData("quay.io/org/img:2", "quay.io/org/")]
    public void PrefixOf_IsTheCanonicalRepositoryUpToItsLastSlash(string image, string prefix) =>
        RegistryTrustBoundary.PrefixOf(image).Should().Be(prefix);

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
    public void From_ReportsWhetherTheListIsTheDefault()
    {
        RegistryTrustBoundary.From(new SandboxGlobalConfig()).Should().BeEquivalentTo(new RegistryTrustBoundary(
            ["mcr.microsoft.com/", "ghcr.io/", "docker.io/library/"], true));

        With("ghcr.io/org").RegistriesAreDefault.Should().BeFalse();
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
