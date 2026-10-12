using AgentSmith.Application.Services.Sandbox.Registry;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox.Registry;

/// <summary>
/// 2026-10-12-50bdb: image references read the way Docker reads them (the same shapes
/// RegistryTrustBoundary's matching reads), and registry challenges read quote by quote.
/// </summary>
public sealed class RegistryParsersTests
{
    private readonly RegistryImageReferenceParser _references = new();

    [Theory]
    [InlineData("node", "registry-1.docker.io", "library/node", "latest")]
    [InlineData("node:20-bookworm", "registry-1.docker.io", "library/node", "20-bookworm")]
    [InlineData("docker.io/node:20", "registry-1.docker.io", "library/node", "20")]
    [InlineData("index.docker.io/library/node:20", "registry-1.docker.io", "library/node", "20")]
    [InlineData("sbtscala/scala-sbt:eclipse-temurin-25_1.11.7_2.13.18", "registry-1.docker.io", "sbtscala/scala-sbt", "eclipse-temurin-25_1.11.7_2.13.18")]
    [InlineData("docker.io/sbtscala/scala-sbt:1", "registry-1.docker.io", "sbtscala/scala-sbt", "1")]
    [InlineData("mcr.microsoft.com/dotnet/sdk:8.0", "mcr.microsoft.com", "dotnet/sdk", "8.0")]
    [InlineData("ghcr.io/org/team/tool", "ghcr.io", "org/team/tool", "latest")]
    [InlineData("localhost:5000/tool:2", "localhost:5000", "tool", "2")]
    public void Parse_KnownShapes_AddressTheRegistryApi(string image, string host, string repository, string reference)
    {
        var parsed = _references.Parse(image)!;

        parsed.ApiHost.Should().Be(host);
        parsed.Repository.Should().Be(repository);
        parsed.Reference.Should().Be(reference);
    }

    [Fact]
    public void Parse_HostPortAndDigest_KeepsPortOutOfTheTag()
    {
        var parsed = _references.Parse("registry.example:5000/team/tool:1.2@sha256:abc")!;

        parsed.ApiHost.Should().Be("registry.example:5000");
        parsed.Repository.Should().Be("team/tool");
        parsed.Reference.Should().Be("sha256:abc");
        parsed.IsDigest.Should().BeTrue();
        parsed.Manifest.ToString().Should().Be("https://registry.example:5000/v2/team/tool/manifests/sha256:abc");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("two words")]
    [InlineData("node:")]
    public void Parse_NotAReference_IsNull(string image) => _references.Parse(image).Should().BeNull();

    [Fact]
    public void ParseChallenge_DockerHubBearer_ReadsRealmServiceAndScope()
    {
        var challenge = new RegistryChallengeParser().Parse(
            "Bearer realm=\"https://auth.docker.io/token\",service=\"registry.docker.io\",scope=\"repository:a/b:pull,push\"")!;

        challenge.Realm.ToString().Should().Be("https://auth.docker.io/token");
        challenge.Service.Should().Be("registry.docker.io");
        challenge.Scope.Should().Be("repository:a/b:pull,push");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Basic realm=\"x\"")]
    [InlineData("Bearer service=\"x\"")]
    [InlineData("Bearer realm=\"file:///etc/passwd\"")]
    public void ParseChallenge_NotAUsableBearer_IsNull(string? header) =>
        new RegistryChallengeParser().Parse(header).Should().BeNull();
}
