using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;
using k8s.Models;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-10-16-8648: a live run's pod failed with "failed: " and nothing else — the
/// toolchain container could not exec the agent, and only the container status said so.
/// </summary>
public sealed class PodStartFailureTests
{
    [Fact]
    public void PodStartFailure_NoPodReason_NamesTheTerminatedContainer()
    {
        var pod = Failed(reason: null, Terminated("toolchain", 127, "Error", "exec /shared/agent: no such file or directory"));

        var message = PodStartFailure.Describe("pod-1", pod);

        message.Should().Be("Sandbox pod pod-1 failed: container toolchain exited 127 (Error)"
            + " — exec /shared/agent: no such file or directory");
    }

    [Fact]
    public void PodStartFailure_PodReasonAndExitedContainer_KeepsBoth()
    {
        var pod = Failed(reason: "Evicted", Terminated("toolchain", 137, "OOMKilled", null));

        PodStartFailure.Describe("pod-1", pod)
            .Should().Be("Sandbox pod pod-1 failed: Evicted; container toolchain exited 137 (OOMKilled)");
    }

    [Fact]
    public void PodStartFailure_NothingReported_SaysSo() =>
        PodStartFailure.Describe("pod-1", Failed(reason: null))
            .Should().EndWith("failed: no reason reported");

    private static V1Pod Failed(string? reason, params V1ContainerStatus[] containers) => new()
    {
        Status = new V1PodStatus { Reason = reason, ContainerStatuses = containers },
    };

    private static V1ContainerStatus Terminated(string name, int exitCode, string? reason, string? message) => new()
    {
        Name = name,
        State = new V1ContainerState
        {
            Terminated = new V1ContainerStateTerminated { ExitCode = exitCode, Reason = reason, Message = message },
        },
    };
}
