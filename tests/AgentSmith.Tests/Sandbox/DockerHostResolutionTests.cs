using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Sandbox;
using Docker.DotNet;
using Docker.DotNet.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0407: the two Docker-host questions that used to live inside the sandbox factory
/// and the registration file — is this socket address usable, and is this image here.
/// </summary>
public sealed class DockerHostResolutionTests
{
    [Fact]
    public void Resolve_WellFormedDockerHost_IsUsedAsIs()
    {
        var findings = new Mock<IStartupFindings>();

        new DockerSocketUriResolver(findings.Object).Resolve("tcp://127.0.0.1:2375")
            .Should().Be(new Uri("tcp://127.0.0.1:2375"));

        findings.Verify(f => f.Record(It.IsAny<StartupFinding>()), Times.Never);
    }

    [Fact]
    public void Resolve_MalformedDockerHost_FallsBackToDefaultSocket_AndRecordsFinding()
    {
        var recorded = new List<StartupFinding>();
        var findings = new Mock<IStartupFindings>();
        findings.Setup(f => f.Record(It.IsAny<StartupFinding>()))
            .Callback<StartupFinding>(recorded.Add);

        var uri = new DockerSocketUriResolver(findings.Object).Resolve("unix //var/run/docker.sock");

        uri.Should().Be(new Uri(DockerSocketUriResolver.DefaultSocket));
        recorded.Should().ContainSingle().Which.Field.Should().Be("DOCKER_HOST");
    }

    [Fact]
    public async Task EnsurePresentAsync_ImagePresent_DoesNotPull()
    {
        var images = BuildImages(present: true, out var pulled);

        await BuildPresence(images).EnsurePresentAsync("node:20", isCarrier: false, CancellationToken.None);

        pulled.Should().BeEmpty();
    }

    [Fact]
    public async Task EnsurePresentAsync_ImageMissing_PullsRepoAndTag()
    {
        var images = BuildImages(present: false, out var pulled);

        await BuildPresence(images).EnsurePresentAsync("alpine:3.20", isCarrier: false, CancellationToken.None);

        pulled.Should().ContainSingle().Which.Should().Be("alpine:3.20");
    }

    [Fact]
    public async Task EnsurePresentAsync_CarrierImageUnpullable_NamesTheBuildCommand()
    {
        var images = BuildImages(present: false, out _);
        Mock.Get(images).Setup(i => i.CreateImageAsync(
                It.IsAny<ImagesCreateParameters>(), It.IsAny<AuthConfig?>(),
                It.IsAny<IProgress<JSONMessage>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DockerApiException(System.Net.HttpStatusCode.NotFound, "no such image"));

        var act = async () => await BuildPresence(images)
            .EnsurePresentAsync("agent-smith-sandbox-agent:latest", isCarrier: true, CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("build sandbox-agent");
    }

    private static DockerImagePresence BuildPresence(IImageOperations images)
    {
        var docker = new Mock<IDockerClient>();
        docker.SetupGet(d => d.Images).Returns(images);
        return new DockerImagePresence(docker.Object, NullLogger<DockerImagePresence>.Instance);
    }

    private static IImageOperations BuildImages(bool present, out List<string> pulled)
    {
        var captured = new List<string>();
        pulled = captured;
        var images = new Mock<IImageOperations>();
        var inspect = images.Setup(i => i.InspectImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()));
        if (present) inspect.ReturnsAsync(new ImageInspectResponse());
        else inspect.ThrowsAsync(new DockerImageNotFoundException(System.Net.HttpStatusCode.NotFound, "missing"));
        images.Setup(i => i.CreateImageAsync(
                It.IsAny<ImagesCreateParameters>(), It.IsAny<AuthConfig?>(),
                It.IsAny<IProgress<JSONMessage>>(), It.IsAny<CancellationToken>()))
            .Callback<ImagesCreateParameters, AuthConfig?, IProgress<JSONMessage>, CancellationToken>(
                (p, _, _, _) => captured.Add($"{p.FromImage}:{p.Tag}"))
            .Returns(Task.CompletedTask);
        return images.Object;
    }
}
