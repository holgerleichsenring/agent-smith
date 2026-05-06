using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Services.Sandbox;
using Docker.DotNet;
using Docker.DotNet.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Sandbox;

public sealed class DockerSandboxFactoryTests
{
    [Fact]
    public async Task CreateAsync_CreatesTwoVolumes_RunsLoaderToCompletion_StartsToolchain()
    {
        var docker = BuildDockerMock(out var calls);
        var factory = new DockerSandboxFactory(
            docker.Object, BuildRedisMock(), new DockerContainerSpecBuilder(),
            new DockerSandboxOptions { RedisUrl = "redis:6379" }, NullLoggerFactory.Instance);

        await using var sandbox = await factory.CreateAsync(
            new SandboxSpec("node:20", "agent:1"), CancellationToken.None);

        calls.VolumesCreated.Should().HaveCount(2);
        calls.VolumesCreated.Should().AllSatisfy(name => name.Should().StartWith("agentsmith-sandbox-"));
        calls.ContainersCreated.Should().HaveCount(2, "loader + toolchain");
        calls.ContainersStarted.Should().HaveCount(2);
        calls.ContainersWaited.Should().ContainSingle("only the loader is awaited");
        calls.ContainersRemoved.Should().ContainSingle("loader is removed after exit");
    }

    [Fact]
    public async Task DisposeAsync_RemovesToolchainContainer_AndBothVolumes()
    {
        var docker = BuildDockerMock(out var calls);
        var factory = new DockerSandboxFactory(
            docker.Object, BuildRedisMock(), new DockerContainerSpecBuilder(),
            new DockerSandboxOptions(), NullLoggerFactory.Instance);

        var sandbox = await factory.CreateAsync(
            new SandboxSpec("node:20", "agent:1"), CancellationToken.None);
        calls.ContainersRemoved.Clear();

        await sandbox.DisposeAsync();

        calls.ContainersRemoved.Should().ContainSingle("toolchain removed on dispose");
        calls.VolumesRemoved.Should().HaveCount(2, "both shared + work volumes removed on dispose");
    }

    private static IConnectionMultiplexer BuildRedisMock()
    {
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(Mock.Of<IDatabase>());
        return redis.Object;
    }

    private static Mock<IDockerClient> BuildDockerMock(out DockerCallTracker tracker)
    {
        tracker = new DockerCallTracker();
        var local = tracker;
        var docker = new Mock<IDockerClient>();
        var containers = new Mock<IContainerOperations>();
        var volumes = new Mock<IVolumeOperations>();

        containers.Setup(c => c.CreateContainerAsync(It.IsAny<CreateContainerParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateContainerParameters p, CancellationToken _) =>
            {
                var id = Guid.NewGuid().ToString("N")[..12];
                local.ContainersCreated.Add((p.Name, id));
                return new CreateContainerResponse { ID = id };
            });
        containers.Setup(c => c.StartContainerAsync(It.IsAny<string>(), It.IsAny<ContainerStartParameters>(), It.IsAny<CancellationToken>()))
            .Callback<string, ContainerStartParameters, CancellationToken>((id, _, _) => local.ContainersStarted.Add(id))
            .ReturnsAsync(true);
        containers.Setup(c => c.WaitContainerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((id, _) => local.ContainersWaited.Add(id))
            .ReturnsAsync(new ContainerWaitResponse { StatusCode = 0 });
        containers.Setup(c => c.RemoveContainerAsync(It.IsAny<string>(), It.IsAny<ContainerRemoveParameters>(), It.IsAny<CancellationToken>()))
            .Callback<string, ContainerRemoveParameters, CancellationToken>((id, _, _) => local.ContainersRemoved.Add(id))
            .Returns(Task.CompletedTask);

        volumes.Setup(v => v.CreateAsync(It.IsAny<VolumesCreateParameters>(), It.IsAny<CancellationToken>()))
            .Callback<VolumesCreateParameters, CancellationToken>((p, _) => local.VolumesCreated.Add(p.Name))
            .ReturnsAsync(new VolumeResponse());
        volumes.Setup(v => v.RemoveAsync(It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
            .Callback<string, bool?, CancellationToken>((name, _, _) => local.VolumesRemoved.Add(name))
            .Returns(Task.CompletedTask);

        docker.SetupGet(d => d.Containers).Returns(containers.Object);
        docker.SetupGet(d => d.Volumes).Returns(volumes.Object);
        return docker;
    }

    private sealed class DockerCallTracker
    {
        public List<(string? Name, string Id)> ContainersCreated { get; } = new();
        public List<string> ContainersStarted { get; } = new();
        public List<string> ContainersWaited { get; } = new();
        public List<string> ContainersRemoved { get; } = new();
        public List<string> VolumesCreated { get; } = new();
        public List<string> VolumesRemoved { get; } = new();
    }
}
