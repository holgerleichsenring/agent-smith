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
            new SandboxSpec("node:20", ResourceLimits.Default, "agent:1"), CancellationToken.None);

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
            new SandboxSpec("node:20", ResourceLimits.Default, "agent:1"), CancellationToken.None);
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
        var images = new Mock<IImageOperations>();

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

        // Default: image is present locally — no pull needed. Individual tests
        // can override Inspect to throw DockerImageNotFoundException to exercise
        // the IfNotPresent pull path.
        images.Setup(i => i.InspectImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((image, _) => local.ImagesInspected.Add(image))
            .ReturnsAsync(new ImageInspectResponse());
        images.Setup(i => i.CreateImageAsync(
                It.IsAny<ImagesCreateParameters>(),
                It.IsAny<AuthConfig?>(),
                It.IsAny<IProgress<JSONMessage>>(),
                It.IsAny<CancellationToken>()))
            .Callback<ImagesCreateParameters, AuthConfig?, IProgress<JSONMessage>, CancellationToken>(
                (p, _, _, _) => local.ImagesPulled.Add($"{p.FromImage}:{p.Tag}"))
            .Returns(Task.CompletedTask);

        docker.SetupGet(d => d.Containers).Returns(containers.Object);
        docker.SetupGet(d => d.Volumes).Returns(volumes.Object);
        docker.SetupGet(d => d.Images).Returns(images.Object);
        return docker;
    }

    [Fact]
    public async Task CreateAsync_ImagePresentLocally_DoesNotPull()
    {
        var docker = BuildDockerMock(out var calls);
        var factory = new DockerSandboxFactory(
            docker.Object, BuildRedisMock(), new DockerContainerSpecBuilder(),
            new DockerSandboxOptions { RedisUrl = "redis:6379" }, NullLoggerFactory.Instance);

        await using var sandbox = await factory.CreateAsync(
            new SandboxSpec("node:20", ResourceLimits.Default, "agent:1"), CancellationToken.None);

        calls.ImagesInspected.Should().Contain(["agent:1", "node:20"]);
        calls.ImagesPulled.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_ToolchainImageMissing_PullsThenCreatesContainer()
    {
        var docker = BuildDockerMock(out var calls);
        OverrideInspectMissing(docker, "alpine:3.20");
        var factory = new DockerSandboxFactory(
            docker.Object, BuildRedisMock(), new DockerContainerSpecBuilder(),
            new DockerSandboxOptions(), NullLoggerFactory.Instance);

        await using var sandbox = await factory.CreateAsync(
            new SandboxSpec("alpine:3.20", ResourceLimits.Default, "agent:1"), CancellationToken.None);

        calls.ImagesPulled.Should().ContainSingle().Which.Should().Be("alpine:3.20");
        calls.ContainersCreated.Should().HaveCount(2, "pull succeeded so loader + toolchain were created");
    }

    [Fact]
    public async Task CreateAsync_LoaderExitsNonZero_ThrowsWithLoaderOutput()
    {
        var docker = BuildDockerMock(out _);
        Mock.Get(docker.Object.Containers)
            .Setup(c => c.WaitContainerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContainerWaitResponse { StatusCode = 1 });
        // Loader log stream is best-effort — leave default unconfigured (returns null/0),
        // ReadContainerLogsAsync wraps the failure and returns empty string.

        var factory = new DockerSandboxFactory(
            docker.Object, BuildRedisMock(), new DockerContainerSpecBuilder(),
            new DockerSandboxOptions(), NullLoggerFactory.Instance);

        var act = async () => await factory.CreateAsync(
            new SandboxSpec("debian:bookworm", ResourceLimits.Default, "agent-smith-sandbox-agent:latest"),
            CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        ex.Message.Should().Contain("loader exited with code 1");
        ex.Message.Should().Contain("agent-smith-sandbox-agent:latest");
    }

    [Fact]
    public async Task CreateAsync_AgentCarrierImageMissingAndPullFails_ThrowsWithBuildHint()
    {
        var docker = BuildDockerMock(out _);
        OverrideInspectMissing(docker, "agent-smith-sandbox-agent:latest");
        OverridePullFails(docker);
        var factory = new DockerSandboxFactory(
            docker.Object, BuildRedisMock(), new DockerContainerSpecBuilder(),
            new DockerSandboxOptions(), NullLoggerFactory.Instance);

        var act = async () => await factory.CreateAsync(
            new SandboxSpec("alpine:3.20", ResourceLimits.Default, "agent-smith-sandbox-agent:latest"),
            CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("sandbox-agent");
    }

    private static void OverrideInspectMissing(Mock<IDockerClient> docker, string image)
    {
        Mock.Get(docker.Object.Images)
            .Setup(i => i.InspectImageAsync(image, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DockerImageNotFoundException(
                System.Net.HttpStatusCode.NotFound,
                $"No such image: {image}"));
    }

    private static void OverridePullFails(Mock<IDockerClient> docker)
    {
        Mock.Get(docker.Object.Images)
            .Setup(i => i.CreateImageAsync(
                It.IsAny<ImagesCreateParameters>(),
                It.IsAny<AuthConfig?>(),
                It.IsAny<IProgress<JSONMessage>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DockerApiException(
                System.Net.HttpStatusCode.NotFound, "image not found in registry"));
    }

    private sealed class DockerCallTracker
    {
        public List<(string? Name, string Id)> ContainersCreated { get; } = new();
        public List<string> ContainersStarted { get; } = new();
        public List<string> ContainersWaited { get; } = new();
        public List<string> ContainersRemoved { get; } = new();
        public List<string> VolumesCreated { get; } = new();
        public List<string> VolumesRemoved { get; } = new();
        public List<string> ImagesInspected { get; } = new();
        public List<string> ImagesPulled { get; } = new();
    }
}
