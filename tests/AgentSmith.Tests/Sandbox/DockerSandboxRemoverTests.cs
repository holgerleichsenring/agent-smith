using AgentSmith.Server.Services.Sandbox;
using Docker.DotNet;
using Docker.DotNet.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Sandbox;

/// <summary>2026-10-25-c5a7b: the orphan remover leaves no volume of a sandbox behind, home included.</summary>
public sealed class DockerSandboxRemoverTests
{
    [Fact]
    public async Task DockerSandboxRemover_RemovesTheHomeVolume()
    {
        var removed = new List<string>();
        var volumes = new Mock<IVolumeOperations>();
        volumes.Setup(v => v.RemoveAsync(It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
            .Callback<string, bool?, CancellationToken>((name, _, _) => removed.Add(name))
            .Returns(Task.CompletedTask);
        var docker = new Mock<IDockerClient>();
        docker.SetupGet(d => d.Containers).Returns(Mock.Of<IContainerOperations>());
        docker.SetupGet(d => d.Volumes).Returns(volumes.Object);

        await new DockerSandboxRemover(docker.Object, NullLogger<DockerSandboxRemover>.Instance)
            .RemoveAsync("container-1", "0123456789abcdef", CancellationToken.None);

        removed.Should().BeEquivalentTo(
            "agentsmith-sandbox-0123456789ab-shared",
            "agentsmith-sandbox-0123456789ab-work",
            "agentsmith-sandbox-0123456789ab-home");
    }
}
