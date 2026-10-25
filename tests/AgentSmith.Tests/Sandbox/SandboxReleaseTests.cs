using AgentSmith.Tests.TestSupport;
using System.Net;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Server.Services.Sandbox;
using AgentSmith.Tests.TestHelpers;
using Docker.DotNet;
using Docker.DotNet.Models;
using FluentAssertions;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-10-13-9a59i: the release reads each sandbox's exit through the backend behind the projector BEFORE
/// it disposes it, and the dispose event says what it read — Running, Exited with facts, or Gone. The real
/// Docker and Kubernetes backends answer the read; the stand-in only records the order and skips their
/// ten-second shutdown grace.
/// </summary>
public sealed class SandboxReleaseTests
{
    private const string RunId = "run-1";
    private const string Key = "api";

    [Fact]
    public async Task SandboxRelease_LiveContainer_PublishesRunningState()
    {
        var disposed = await ReleaseAsync(DockerReading(new ContainerState { Running = true }));

        disposed.ExitState.Should().Be("running");
        (disposed.ExitCode, disposed.OomKilled).Should().Be((null, null));
    }

    [Fact]
    public async Task SandboxRelease_DockerContainerExited_PublishesExitedWithCode()
    {
        var disposed = await ReleaseAsync(DockerReading(new ContainerState { Running = false, ExitCode = 139, Status = "exited" }));

        (disposed.ExitState, disposed.ExitCode, disposed.OomKilled).Should().Be(("exited", 139, false));
    }

    [Fact]
    public async Task SandboxRelease_DockerContainerGone_PublishesGone()
    {
        var disposed = await ReleaseAsync(DockerReading(state: null, notFound: true));

        disposed.ExitState.Should().Be("gone");
        disposed.ExitCode.Should().BeNull();
    }

    [Fact]
    public async Task SandboxRelease_KubernetesPodOomKilled_PublishesOomKilledWithExitCode()
    {
        var pod = new V1Pod
        {
            Status = new V1PodStatus
            {
                ContainerStatuses =
                [
                    new V1ContainerStatus
                    {
                        Name = "toolchain",
                        State = new V1ContainerState
                        {
                            Terminated = new V1ContainerStateTerminated { ExitCode = 137, Reason = "OOMKilled" },
                        },
                    },
                ],
            },
        };

        var disposed = await ReleaseAsync(KubernetesReading(pod));

        (disposed.ExitState, disposed.ExitCode, disposed.OomKilled).Should().Be(("exited", 137, true));
    }

    [Fact]
    public async Task SandboxRelease_KubernetesPodDeleted_PublishesGone()
    {
        var disposed = await ReleaseAsync(KubernetesReading(pod: null));

        disposed.ExitState.Should().Be("gone");
    }

    [Fact]
    public async Task SandboxRelease_InProcess_PublishesNoEvidence()
    {
        var disposed = await ReleaseAsync(reader: null);

        (disposed.ExitState, disposed.ExitCode, disposed.OomKilled).Should().Be((null, null, null));
    }

    private static async Task<SandboxDisposedEvent> ReleaseAsync(ISandboxExitReader? reader)
    {
        var steps = new List<string>();
        ISandbox backend = reader is null ? new RecordingBackend(steps) : new ReadingBackend(reader, steps);
        var wrapped = new SandboxEventProjector(backend, EventTestStubs.NoOp, EventTestStubs.RunContext, Key);
        var events = EventTestStubs.Recording();
        var release = new SandboxRelease(events, NullLogger<SandboxRelease>.Instance);

        await release.ReleaseAllAsync(RunId, new Dictionary<string, ISandbox> { [Key] = wrapped });

        steps.Should().Equal(reader is null ? ["dispose"] : ["read", "dispose"]);
        return events.Events.OfType<SandboxDisposedEvent>().Should().ContainSingle(e => e.RunId == RunId && e.Repo == Key).Subject;
    }

    private static DockerSandbox DockerReading(ContainerState? state, bool notFound = false)
    {
        var containers = new Mock<IContainerOperations>();
        var inspect = containers.Setup(c => c.InspectContainerAsync("toolchain-1", It.IsAny<CancellationToken>()));
        if (notFound) inspect.ThrowsAsync(new DockerContainerNotFoundException(HttpStatusCode.NotFound, "gone"));
        else inspect.ReturnsAsync(new ContainerInspectResponse { State = state });
        var docker = new Mock<IDockerClient>();
        docker.SetupGet(d => d.Containers).Returns(containers.Object);
        return new DockerSandbox(docker.Object, "toolchain-1", ["shared", "work"], "job-1", Channel(), SandboxBusDoubles.SharedCredential,
            stepTimeoutCapSeconds: 900, "node:20", NullLogger.Instance);
    }

    private static KubernetesSandbox KubernetesReading(V1Pod? pod)
    {
        var core = new Mock<ICoreV1Operations>();
        var read = core.Setup(c => c.ReadNamespacedPodWithHttpMessagesAsync(
            "pod-1", "agentsmith", It.IsAny<bool?>(),
            It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(), It.IsAny<CancellationToken>()));
        if (pod is null)
            read.ThrowsAsync(new HttpOperationException("not found")
            {
                Response = new HttpResponseMessageWrapper(new HttpResponseMessage(HttpStatusCode.NotFound), string.Empty),
            });
        else read.ReturnsAsync(new HttpOperationResponse<V1Pod> { Body = pod });
        var client = new Mock<IKubernetes>();
        client.SetupGet(c => c.CoreV1).Returns(core.Object);
        return new KubernetesSandbox(client.Object, "agentsmith", "pod-1", "job-1", Channel(), SandboxBusDoubles.SharedCredential,
            stepTimeoutCapSeconds: 900, "node:20", ResolvedSandboxSecrets.Empty, NullLogger.Instance);
    }

    private static SandboxRedisChannel Channel()
    {
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(Mock.Of<IDatabase>());
        return new SandboxRedisChannel(redis.Object, "job-1", NullLogger.Instance, Mock.Of<IWireProtocolWatcher>());
    }

    /// <summary>A backend that only records its dispose — the in-process shape, no process to read.</summary>
    private class RecordingBackend(List<string> steps) : ISandbox
    {
        public string JobId => "job-1";

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync()
        {
            steps.Add("dispose");
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>A backend whose exit read is a real backend's, skipping that backend's shutdown grace.</summary>
    private sealed class ReadingBackend(ISandboxExitReader reader, List<string> steps)
        : RecordingBackend(steps), ISandboxExitReader
    {
        public Task<SandboxExitRead> ReadExitAsync(CancellationToken cancellationToken)
        {
            steps.Add("read");
            return reader.ReadExitAsync(cancellationToken);
        }
    }
}
