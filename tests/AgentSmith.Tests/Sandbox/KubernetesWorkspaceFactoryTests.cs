using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Services.Sandbox;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Moq;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-11-25-9992b: a run's Kubernetes workspace is ONE pod with a toolchain container per image. It is ready when
/// every container is, each container sandbox reads its own exit, and the pod is deleted once — by the workspace,
/// after every container's agent ended.
/// </summary>
public sealed class KubernetesWorkspaceFactoryTests
{
    private static readonly WorkspaceSpec TwoImages = new(
        new SandboxSpec("ignored", ResourceLimits.Default, "agent:1", TimeoutSeconds: 5),
        [new WorkspaceToolchain("dotnet:9", ResourceLimits.Default), new WorkspaceToolchain("node:20", ResourceLimits.Default)]);

    private readonly List<V1Pod> _created = [];
    private readonly List<string> _deleted = [];
    private V1PodStatus _status = Status(Ready("toolchain-0"), Ready("toolchain-1"));

    [Fact]
    public async Task CreateAsync_TwoImages_OnePodWithAContainerPerImage()
    {
        await using var workspace = await Factory().CreateAsync(TwoImages, CancellationToken.None);

        var pod = _created.Should().ContainSingle().Subject;
        pod.Spec.Containers.Select(c => c.Image).Should().Equal("dotnet:9", "node:20");
        workspace.Images.Should().Equal("dotnet:9", "node:20");
        workspace.Container("dotnet:9").JobId.Should().NotBe(workspace.Container("node:20").JobId);
        SandboxPodCandidates.JobIds(pod).Should().Equal(workspace.Container("dotnet:9").JobId, workspace.Container("node:20").JobId);
    }

    [Fact]
    public async Task ReadExit_EachContainer_ReadsItsOwnStatus()
    {
        await using var workspace = await Factory().CreateAsync(TwoImages, CancellationToken.None);
        _status = Status(Ready("toolchain-0"), new V1ContainerStatus
        {
            Name = "toolchain-1",
            State = new V1ContainerState { Terminated = new V1ContainerStateTerminated { ExitCode = 137, Reason = "OOMKilled" } },
        });

        var dotnet = await ((ISandboxExitReader)workspace.Container("dotnet:9")).ReadExitAsync(CancellationToken.None);
        var node = await ((ISandboxExitReader)workspace.Container("node:20")).ReadExitAsync(CancellationToken.None);

        dotnet.Should().Be(SandboxExitRead.Running);
        node.Should().Be(SandboxExitRead.Exited(137, oomKilled: true));
    }

    [Fact]
    public async Task DisposeAsync_DeletesThePodOnce_AfterTheContainers_NeverAContainerAlone()
    {
        var workspace = await Factory().CreateAsync(TwoImages, CancellationToken.None);

        await ((ISandboxForceRemoval)workspace.Container("node:20")).ForceRemoveAsync(CancellationToken.None);
        _deleted.Should().BeEmpty("a workspace container never removes the pod its siblings run in");
        await workspace.DisposeAsync();
        await workspace.DisposeAsync();

        _deleted.Should().ContainSingle().Which.Should().Be(_created.Single().Metadata.Name);
    }

    private KubernetesWorkspaceFactory Factory() => KubernetesFactoryDoubles.Workspace(
        Client(), DockerWorkspaceFactoryTests.RefusingRedis(), new KubernetesSandboxOptions(), SandboxBusDoubles.Credentials());

    private IKubernetes Client()
    {
        var core = new Mock<ICoreV1Operations>();
        core.Setup(c => c.CreateNamespacedPodWithHttpMessagesAsync(
                It.IsAny<V1Pod>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool?>(), It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(), It.IsAny<CancellationToken>()))
            .Callback<V1Pod, string, string, string, string, bool?, IReadOnlyDictionary<string, IReadOnlyList<string>>,
                CancellationToken>((pod, _, _, _, _, _, _, _) => _created.Add(pod))
            .ReturnsAsync((V1Pod pod, string _, string _, string _, string _, bool? _,
                IReadOnlyDictionary<string, IReadOnlyList<string>> _, CancellationToken _) =>
                new HttpOperationResponse<V1Pod> { Body = pod });
        core.Setup(c => c.ReadNamespacedPodWithHttpMessagesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>(),
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new HttpOperationResponse<V1Pod> { Body = new V1Pod { Status = _status } });
        core.Setup(c => c.DeleteNamespacedPodWithHttpMessagesAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<V1DeleteOptions>(), It.IsAny<string>(), It.IsAny<int?>(),
                It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<string>(), It.IsAny<bool?>(),
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, V1DeleteOptions, string, int?, bool?, bool?, string, bool?,
                IReadOnlyDictionary<string, IReadOnlyList<string>>, CancellationToken>(
                (name, _, _, _, _, _, _, _, _, _, _) => _deleted.Add(name))
            .ReturnsAsync(new HttpOperationResponse<V1Pod> { Body = new V1Pod() });
        var client = new Mock<IKubernetes>();
        client.SetupGet(c => c.CoreV1).Returns(core.Object);
        return client.Object;
    }

    private static V1PodStatus Status(params V1ContainerStatus[] containers) =>
        new() { ContainerStatuses = [.. containers] };

    private static V1ContainerStatus Ready(string name) =>
        new() { Name = name, Ready = true, State = new V1ContainerState { Running = new V1ContainerStateRunning() } };
}
