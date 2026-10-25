using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Services.Sandbox;
using AgentSmith.Tests.TestSupport;
using Docker.DotNet;
using Docker.DotNet.Models;
using FluentAssertions;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-10-25-c5a7e: every way a sandbox goes deletes its per-job Redis user: the
/// sandbox's own teardown, the Docker orphan remover and the Kubernetes corpse sweep.
/// </summary>
public sealed class SandboxBusUserRemovalTests
{
    [Fact]
    public async Task KubernetesSandbox_ForceRemove_DeletesTheUser()
    {
        var redis = new SandboxBusRedisDouble();
        var bus = await SandboxBusCredentialsTests.Credentials(redis).IssueAsync("job-1", "redis:6379");
        var sandbox = new KubernetesSandbox(KubernetesClient().Object, "agentsmith", "pod-1", "job-1",
            new SandboxRedisChannel(redis.Multiplexer.Object, "job-1", NullLogger.Instance, Mock.Of<IWireProtocolWatcher>()),
            bus, 900, "node:20", ResolvedSandboxSecrets.Empty, NullLogger.Instance);

        await sandbox.ForceRemoveAsync(CancellationToken.None);

        redis.Calls("DELUSER").Should().ContainSingle().Which.Should().Equal("DELUSER", "sbx-job-1");
    }

    [Fact]
    public async Task DockerSandboxRemover_Reap_DeletesTheUser()
    {
        var redis = new SandboxBusRedisDouble();
        var docker = new Mock<IDockerClient>();
        docker.SetupGet(d => d.Containers).Returns(Mock.Of<IContainerOperations>());
        docker.SetupGet(d => d.Volumes).Returns(Mock.Of<IVolumeOperations>());
        var remover = new DockerSandboxRemover(docker.Object,
            SandboxBusCredentialsTests.Credentials(redis), NullLogger<DockerSandboxRemover>.Instance);

        await remover.RemoveAsync("container-1", "job-1", CancellationToken.None);

        redis.Calls("DELUSER").Should().ContainSingle().Which.Should().Equal("DELUSER", "sbx-job-1");
    }

    [Fact]
    public async Task KubernetesSandboxCorpseReaper_Reap_DeletesTheUserFromThePipelineIdLabel()
    {
        var redis = new SandboxBusRedisDouble();
        var client = KubernetesClient(Corpse("agentsmith-sandbox-dead", jobId: "job-dead"));
        var reaper = new KubernetesSandboxCorpseReaper(client.Object, new KubernetesSandboxOptions(),
            new SandboxPodLabels(new SandboxOwnerIdentity("owner")),
            new LiveRunSetReader(InMemoryRedis.Connection(), new AgentSmith.Application.Services.Claim.NoOpActiveRunLease(),
                new AgentSmith.Application.Services.Lifecycle.NoOpRunHeartbeat(), NullLogger<LiveRunSetReader>.Instance),
            SandboxHoldRailDoubles.Reader(), SandboxBusCredentialsTests.Credentials(redis),
            NullLogger<KubernetesSandboxCorpseReaper>.Instance);

        var reaped = await reaper.ReapCorpsesAsync(CancellationToken.None);

        // The first sweep also lists the unowned selector, which this client answers alike.
        reaped.Should().BePositive();
        redis.Calls("DELUSER").Should().NotBeEmpty().And.OnlyContain(args => (string)args[1] == "sbx-job-dead");
    }

    private static V1Pod Corpse(string name, string jobId) => new()
    {
        Metadata = new V1ObjectMeta
        {
            Name = name,
            Labels = new Dictionary<string, string>
            {
                ["app"] = SandboxPodLabels.AppLabel,
                [SandboxPodLabels.PipelineIdLabel] = jobId,
                [SandboxPodLabels.RunIdLabel] = "run-gone",
            },
            CreationTimestamp = DateTime.UtcNow.AddHours(-1),
        },
    };

    private static Mock<IKubernetes> KubernetesClient(params V1Pod[] pods)
    {
        var core = new Mock<ICoreV1Operations>();
        core.Setup(c => c.ListNamespacedPodWithHttpMessagesAsync(
                It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<int?>(),
                It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpOperationResponse<V1PodList> { Body = new V1PodList { Items = [.. pods] } });
        core.Setup(c => c.DeleteNamespacedPodWithHttpMessagesAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<V1DeleteOptions>(),
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<bool?>(), It.IsAny<bool?>(),
                It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpOperationResponse<V1Pod> { Body = new V1Pod() });
        var client = new Mock<IKubernetes>();
        client.SetupGet(c => c.CoreV1).Returns(core.Object);
        return client;
    }
}
