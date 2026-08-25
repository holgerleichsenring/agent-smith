using System.Text.Json;
using AgentSmith.Sandbox.Agent.Services;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Sandbox.Agent.Tests.Services;

/// <summary>
/// 2026-08-25-0d01: what the agent does INSTEAD of dying. A protocol difference that ends
/// the process is not reported as a protocol difference — <c>SandboxLivenessWatcher</c>
/// reads the exit as "sandbox vanished" and cancels the whole run. So the agent has to
/// stay alive and answer.
/// </summary>
public sealed class UnknownMessageSurvivalTests
{
    [Fact]
    public async Task Agent_ReceivingAnUnknownMessageKind_DoesNotExit()
    {
        var bus = new Mock<IRedisJobBus>();
        var queue = new Queue<Step>([
            new Step(WireProtocol.Current, Guid.NewGuid(), StepKind.Unknown),
            Step.Shutdown(Guid.NewGuid())
        ]);
        bus.Setup(b => b.WaitForStepAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => queue.Count > 0 ? queue.Dequeue() : null);

        var exit = await Loop(bus).RunAsync("job-1", CancellationToken.None);

        exit.Should().Be(JobLoop.ExitOk, "the loop kept running and ended on the shutdown step");
        exit.Should().NotBe(Program.ExitUnhandledError,
            "the exit the liveness watcher reads as a dead sandbox");
    }

    [Fact]
    public async Task Agent_ReceivingAnUnknownMessageKind_IsNotReportedAsVanished()
    {
        var bus = new Mock<IRedisJobBus>();
        var stepId = Guid.NewGuid();
        var queue = new Queue<Step>([
            new Step(WireProtocol.Current, stepId, StepKind.Unknown), Step.Shutdown(Guid.NewGuid())
        ]);
        bus.Setup(b => b.WaitForStepAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => queue.Count > 0 ? queue.Dequeue() : null);
        StepResult? answered = null;
        bus.Setup(b => b.PushResultAsync("job-1", It.IsAny<StepResult>(), It.IsAny<CancellationToken>()))
            .Callback((string _, StepResult r, CancellationToken _) => answered = r)
            .Returns(Task.CompletedTask);

        await Loop(bus).RunAsync("job-1", CancellationToken.None);

        answered.Should().NotBeNull("the server gets an answer it can read, not a silence "
            + "it can only interpret as a death");
        answered!.StepId.Should().Be(stepId);
        answered.ErrorMessage.Should().Contain(WireProtocol.Window);
    }

    // A message that is not a step at all fails in the same place the unknown kind used to,
    // with the same ending. It is discarded as an idle cycle instead.
    [Fact]
    public async Task Agent_ReceivingAnUnreadableMessage_CountsAnIdleCycleInsteadOfDying()
    {
        var bus = new Mock<IRedisJobBus>();
        var thrown = 0;
        bus.Setup(b => b.WaitForStepAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(() => ++thrown == 1
                ? throw new JsonException("not a step")
                : Task.FromResult<Step?>(Step.Shutdown(Guid.NewGuid())));

        var exit = await Loop(bus).RunAsync("job-1", CancellationToken.None);

        exit.Should().Be(JobLoop.ExitOk);
        thrown.Should().Be(2, "the unreadable message was discarded and the loop asked again");
    }

    private static JobLoop Loop(Mock<IRedisJobBus> bus) => new(
        bus.Object, new ToleratedStepReader(bus.Object, NullLogger<ToleratedStepReader>.Instance),
        Mock.Of<IStepExecutor>(), NullStepInFlightMarker.Instance, NullLogger<JobLoop>.Instance);
}
