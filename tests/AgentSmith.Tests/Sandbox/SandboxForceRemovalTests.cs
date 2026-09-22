using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Server.Services.Sandbox;
using Docker.DotNet;
using Docker.DotNet.Models;
using FluentAssertions;
using k8s;
using k8s.Autorest;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: releasing a held sandbox force-removes it — no shutdown step, and
/// so none of the flat ten-second grace that follows one. A disposal pays that grace
/// per sandbox, serially, which is thirty seconds for a three-repository conversation
/// on a capacity door an operator is waiting at.
/// </summary>
public sealed class SandboxForceRemovalTests
{
    [Fact]
    public async Task DockerSandbox_ForceRemove_RemovesTheContainerWithoutPushingAShutdownStep()
    {
        var removed = new List<string>();
        var docker = DockerThatRecordsRemovals(removed);
        var database = new Mock<IDatabase>();
        var sandbox = new DockerSandbox(
            docker, "toolchain-1", "shared-vol", "work-vol", "job-1", Channel(database, "job-1"),
            stepTimeoutCapSeconds: 900, "node:20", NullLogger.Instance);

        await ((ISandboxForceRemoval)sandbox).ForceRemoveAsync(CancellationToken.None);

        removed.Should().ContainSingle().Which.Should().Be("toolchain-1");
        database.Verify(
            d => d.ListLeftPushAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<When>(), It.IsAny<CommandFlags>()),
            Times.Never,
            "no shutdown step is pushed, so nothing waits out the grace that follows one");
    }

    [Fact]
    public async Task KubernetesSandbox_ForceRemove_DeletesThePodWithoutPushingAShutdownStep()
    {
        var database = new Mock<IDatabase>();
        var client = KubernetesThatAcceptsADelete(out var deleted);
        var sandbox = new KubernetesSandbox(
            client, "agentsmith", "pod-1", "job-1", Channel(database, "job-1"),
            stepTimeoutCapSeconds: 900, "node:20", ResolvedSandboxSecrets.Empty, NullLogger.Instance);

        await ((ISandboxForceRemoval)sandbox).ForceRemoveAsync(CancellationToken.None);

        deleted.Should().ContainSingle().Which.Should().Be("pod-1");
        database.Verify(
            d => d.ListLeftPushAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<When>(), It.IsAny<CommandFlags>()),
            Times.Never);
    }

    private static SandboxRedisChannel Channel(Mock<IDatabase> database, string jobId)
    {
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        return new SandboxRedisChannel(
            redis.Object, jobId, NullLogger.Instance, Mock.Of<IWireProtocolWatcher>());
    }

    private static IDockerClient DockerThatRecordsRemovals(List<string> removed)
    {
        var containers = new Mock<IContainerOperations>();
        containers.Setup(c => c.RemoveContainerAsync(
                It.IsAny<string>(), It.IsAny<ContainerRemoveParameters>(), It.IsAny<CancellationToken>()))
            .Callback<string, ContainerRemoveParameters, CancellationToken>((id, _, _) => removed.Add(id))
            .Returns(Task.CompletedTask);
        var volumes = new Mock<IVolumeOperations>();
        volumes.Setup(v => v.RemoveAsync(It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var docker = new Mock<IDockerClient>();
        docker.SetupGet(d => d.Containers).Returns(containers.Object);
        docker.SetupGet(d => d.Volumes).Returns(volumes.Object);
        return docker.Object;
    }

    private static IKubernetes KubernetesThatAcceptsADelete(out List<string> deleted)
    {
        var names = new List<string>();
        deleted = names;
        var core = new Mock<ICoreV1Operations>();
        core.Setup(c => c.DeleteNamespacedPodWithHttpMessagesAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<k8s.Models.V1DeleteOptions>(),
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<bool?>(), It.IsAny<bool?>(),
                It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(),
                It.IsAny<CancellationToken>()))
            .Callback((string name, string _, k8s.Models.V1DeleteOptions _, string _, int? _, bool? _, bool? _,
                    string _, bool? _, IReadOnlyDictionary<string, IReadOnlyList<string>> _, CancellationToken _) =>
                names.Add(name))
            .ReturnsAsync(new HttpOperationResponse<k8s.Models.V1Pod> { Body = new k8s.Models.V1Pod() });
        var client = new Mock<IKubernetes>();
        client.SetupGet(c => c.CoreV1).Returns(core.Object);
        return client.Object;
    }
}
