using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Services.Sandbox;
using AgentSmith.Tests.TestSupport;
using Docker.DotNet;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-11-25-9992b: a run's Docker workspace is ONE volume set and ONE loader with a toolchain container per image,
/// each its own job; a hardened profile gives every container its own home. The volumes go once, with the workspace.
/// </summary>
public sealed class DockerWorkspaceFactoryTests
{
    private static readonly WorkspaceSpec TwoImages = new(
        new SandboxSpec("ignored", ResourceLimits.Default, "agent:1", SecurityContext: SandboxSecurityContext.Hardened,
            RunId: "run-1"),
        [new WorkspaceToolchain("dotnet:9", ResourceLimits.Default), new WorkspaceToolchain("node:20", ResourceLimits.Default)]);

    [Fact]
    public async Task CreateAsync_TwoImages_OneVolumeSetOneLoaderAndAContainerPerImage()
    {
        var docker = DockerSandboxFactoryTests.BuildDockerMock(out var calls);

        await using var workspace = await Factory(docker).CreateAsync(TwoImages, CancellationToken.None);

        var sandboxVolumes = calls.VolumesCreated.Where(n => n.StartsWith("agentsmith-sandbox-")).ToList();
        sandboxVolumes.Should().ContainSingle(n => n.EndsWith("-shared"));
        sandboxVolumes.Should().ContainSingle(n => n.EndsWith("-work"));
        sandboxVolumes.Count(n => n.EndsWith("-home")).Should().Be(2, "every container has a home of its own");
        calls.Specs.Should().ContainSingle(p => p.Name.Contains("-loader-"));
        var toolchains = calls.Specs.Where(p => !p.Name.Contains("-loader-") && !p.Name.Contains("-owner-")).ToList();
        toolchains.Select(p => p.Image).Should().Equal("dotnet:9", "node:20");
        toolchains.Select(p => p.Name).Should().OnlyHaveUniqueItems().And.OnlyContain(n => n.StartsWith("agentsmith-sandbox-"));
        toolchains.Select(p => p.Labels[DockerContainerSpecBuilder.JobIdLabel]).Should().OnlyHaveUniqueItems();
        var work = sandboxVolumes.Single(n => n.EndsWith("-work"));
        toolchains.Should().OnlyContain(p => p.HostConfig.Binds.Any(b => b.StartsWith(work + ":/work")));
        workspace.Container("dotnet:9").JobId.Should().NotBe(workspace.Container("node:20").JobId);
        workspace.Serves("python:3").Should().BeFalse();
    }

    [Fact]
    public async Task DisposeAsync_RemovesEveryContainerThenTheVolumesOnce()
    {
        var docker = DockerSandboxFactoryTests.BuildDockerMock(out var calls);
        var workspace = await Factory(docker).CreateAsync(TwoImages, CancellationToken.None);
        calls.ContainersRemoved.Clear();

        await workspace.DisposeAsync();
        await workspace.DisposeAsync();

        calls.ContainersRemoved.Should().HaveCount(2);
        calls.VolumesRemoved.Should().HaveCount(4, "shared, work and the two homes, each once")
            .And.OnlyHaveUniqueItems();
    }

    private static DockerWorkspaceFactory Factory(Mock<IDockerClient> docker)
    {
        var options = new DockerSandboxOptions();
        return new DockerWorkspaceFactory(docker.Object, RefusingRedis(),
            new DockerContainerSpecBuilder(new SandboxOwnerIdentity("store-test"), new DockerHardening(options)), options,
            new DockerPackageCaches(docker.Object, options, NullLogger<DockerPackageCaches>.Instance),
            new DockerImagePresence(docker.Object, NullLogger<DockerImagePresence>.Instance),
            new DockerSandboxVolumes(docker.Object, DockerSandboxFactoryTests.OneShot(docker)),
            DockerSandboxFactoryTests.OneShot(docker), Options.Create(new SandboxGlobalConfig()),
            Mock.Of<IWireProtocolWatcher>(), SandboxBusDoubles.Credentials(), NullLoggerFactory.Instance);
    }

    // The shutdown step cannot be pushed, so a disposing container skips its grace instead of waiting it out.
    internal static IConnectionMultiplexer RefusingRedis()
    {
        var database = new Mock<IDatabase>();
        database.Setup(d => d.ListLeftPushAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "no redis in this test"));
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        return redis.Object;
    }
}
