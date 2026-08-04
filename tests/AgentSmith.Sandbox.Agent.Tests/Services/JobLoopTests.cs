using AgentSmith.Sandbox.Wire;
using AgentSmith.Sandbox.Agent.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Sandbox.Agent.Tests.Services;

public class JobLoopTests
{
    [Fact]
    public async Task RunAsync_ThreeRunsThenShutdown_ExecutesAllAndExitsZero()
    {
        var bus = new Mock<IRedisJobBus>();
        var executor = new Mock<IStepExecutor>();
        var queue = new Queue<Step>(new[]
        {
            MakeRunStep("echo", "1"), MakeRunStep("echo", "2"), MakeRunStep("echo", "3"),
            Step.Shutdown(Guid.NewGuid())
        });
        bus.Setup(b => b.WaitForStepAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => queue.Count > 0 ? queue.Dequeue() : null);
        executor.Setup(e => e.ExecuteAsync(It.IsAny<Step>(), It.IsAny<Func<IReadOnlyList<StepEvent>, Task>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Step s, Func<IReadOnlyList<StepEvent>, Task> _, CancellationToken _) =>
                new StepResult(StepResult.CurrentSchemaVersion, s.StepId, 0, false, 0.1, null));

        var loop = new JobLoop(bus.Object, executor.Object, NullStepInFlightMarker.Instance, NullLogger<JobLoop>.Instance);
        var exit = await loop.RunAsync("job-1", CancellationToken.None);

        exit.Should().Be(JobLoop.ExitOk);
        executor.Verify(e => e.ExecuteAsync(It.IsAny<Step>(), It.IsAny<Func<IReadOnlyList<StepEvent>, Task>>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        bus.Verify(b => b.PushResultAsync("job-1", It.IsAny<StepResult>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task RunAsync_TimedOutStep_ContinuesLoop()
    {
        var bus = new Mock<IRedisJobBus>();
        var executor = new Mock<IStepExecutor>();
        var queue = new Queue<Step>(new[] { MakeRunStep("sleep", "5"), Step.Shutdown(Guid.NewGuid()) });
        bus.Setup(b => b.WaitForStepAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => queue.Count > 0 ? queue.Dequeue() : null);
        executor.Setup(e => e.ExecuteAsync(It.IsAny<Step>(), It.IsAny<Func<IReadOnlyList<StepEvent>, Task>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Step s, Func<IReadOnlyList<StepEvent>, Task> _, CancellationToken _) =>
                new StepResult(StepResult.CurrentSchemaVersion, s.StepId, -1, true, 1.0, "timed out"));

        var loop = new JobLoop(bus.Object, executor.Object, NullStepInFlightMarker.Instance, NullLogger<JobLoop>.Instance);
        var exit = await loop.RunAsync("job-1", CancellationToken.None);

        exit.Should().Be(JobLoop.ExitOk);
        bus.Verify(b => b.PushResultAsync("job-1", It.Is<StepResult>(r => r.TimedOut), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_RunStepWithoutCommand_PushesFailureAndContinues()
    {
        var bus = new Mock<IRedisJobBus>();
        var executor = new Mock<IStepExecutor>();
        var invalid = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, Command: null);
        var queue = new Queue<Step>(new[] { invalid, Step.Shutdown(Guid.NewGuid()) });
        bus.Setup(b => b.WaitForStepAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => queue.Count > 0 ? queue.Dequeue() : null);

        var loop = new JobLoop(bus.Object, executor.Object, NullStepInFlightMarker.Instance, NullLogger<JobLoop>.Instance);
        var exit = await loop.RunAsync("job-1", CancellationToken.None);

        exit.Should().Be(JobLoop.ExitOk);
        executor.Verify(e => e.ExecuteAsync(It.IsAny<Step>(), It.IsAny<Func<IReadOnlyList<StepEvent>, Task>>(), It.IsAny<CancellationToken>()), Times.Never);
        bus.Verify(b => b.PushResultAsync("job-1", It.Is<StepResult>(r => r.ExitCode == -1 && !r.TimedOut), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_FiveIdleCycles_ExitsWithCodeTwo()
    {
        var bus = new Mock<IRedisJobBus>();
        var executor = new Mock<IStepExecutor>();
        bus.Setup(b => b.WaitForStepAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Step?)null);

        var loop = new JobLoop(bus.Object, executor.Object, NullStepInFlightMarker.Instance, NullLogger<JobLoop>.Instance);
        var exit = await loop.RunAsync("job-1", CancellationToken.None);

        exit.Should().Be(JobLoop.ExitIdleTimeout);
        bus.Verify(b => b.WaitForStepAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Exactly(JobLoop.MaxIdleCycles));
    }

    [Fact]
    public async Task RunAsync_IdleLimitButRunStillActive_KeepsWaitingInsteadOfExiting()
    {
        // p0360b: the idle exit is a backstop for a dead SERVER. A multi-repo run
        // legitimately leaves a sandbox idle >30 min; while the run is in the
        // active set the agent must keep waiting. First limit: run alive → reset;
        // second limit: run gone → exit 2.
        var bus = new Mock<IRedisJobBus>();
        var executor = new Mock<IStepExecutor>();
        bus.Setup(b => b.WaitForStepAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Step?)null);
        bus.SetupSequence(b => b.IsRunActiveAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);

        var loop = new JobLoop(
            bus.Object, executor.Object, NullStepInFlightMarker.Instance,
            NullLogger<JobLoop>.Instance, runId: "run-1");
        var exit = await loop.RunAsync("job-1", CancellationToken.None);

        exit.Should().Be(JobLoop.ExitIdleTimeout);
        bus.Verify(b => b.WaitForStepAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Exactly(JobLoop.MaxIdleCycles * 2), "the alive probe must grant a full second idle window");
        bus.Verify(b => b.IsRunActiveAsync("run-1", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RunAsync_IdleLimitAliveProbeThrows_ExitsAsBackstop()
    {
        // Redis error at the probe → fail-closed to the original backstop (exit 2).
        var bus = new Mock<IRedisJobBus>();
        var executor = new Mock<IStepExecutor>();
        bus.Setup(b => b.WaitForStepAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Step?)null);
        bus.Setup(b => b.IsRunActiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis down"));

        var loop = new JobLoop(
            bus.Object, executor.Object, NullStepInFlightMarker.Instance,
            NullLogger<JobLoop>.Instance, runId: "run-1");
        var exit = await loop.RunAsync("job-1", CancellationToken.None);

        exit.Should().Be(JobLoop.ExitIdleTimeout);
    }

    [Fact]
    public async Task RunAsync_Cancellation_PropagatesAsOperationCanceledException()
    {
        var bus = new Mock<IRedisJobBus>();
        var executor = new Mock<IStepExecutor>();
        using var cts = new CancellationTokenSource();
        bus.Setup(b => b.WaitForStepAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, TimeSpan _, CancellationToken ct) =>
            {
                await cts.CancelAsync();
                ct.ThrowIfCancellationRequested();
                return null;
            });

        var loop = new JobLoop(bus.Object, executor.Object, NullStepInFlightMarker.Instance, NullLogger<JobLoop>.Instance);
        var act = () => loop.RunAsync("job-1", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // p0396: a transient Redis hiccup during the idle wait (one blocking RPOP
    // exceeding the client timeout) must NOT kill the agent — it counts as an
    // idle cycle and the loop continues. The 30-cycle budget still bounds a
    // Redis that stays gone; everything non-transient stays fatal.
    [Fact]
    public async Task JobLoop_RedisTimeoutDuringWait_ContinuesAndCountsIdleCycle()
    {
        var bus = new Mock<IRedisJobBus>();
        var executor = new Mock<IStepExecutor>();
        bus.SetupSequence(b => b.WaitForStepAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RedisTimeoutException("Timeout awaiting response", CommandStatus.Unknown))
            .ReturnsAsync(Step.Shutdown(Guid.NewGuid()));

        var loop = new JobLoop(bus.Object, executor.Object, NullStepInFlightMarker.Instance, NullLogger<JobLoop>.Instance);
        var exit = await loop.RunAsync("job-1", CancellationToken.None);

        exit.Should().Be(JobLoop.ExitOk, "a 6.5s Redis stall during idle wait must leave the agent alive");
        bus.Verify(b => b.WaitForStepAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task JobLoop_RedisConnectionExceptionDuringWait_ContinuesAndCountsIdleCycle()
    {
        var bus = new Mock<IRedisJobBus>();
        var executor = new Mock<IStepExecutor>();
        bus.SetupSequence(b => b.WaitForStepAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "connection lost"))
            .ReturnsAsync(Step.Shutdown(Guid.NewGuid()));

        var loop = new JobLoop(bus.Object, executor.Object, NullStepInFlightMarker.Instance, NullLogger<JobLoop>.Instance);
        var exit = await loop.RunAsync("job-1", CancellationToken.None);

        exit.Should().Be(JobLoop.ExitOk);
    }

    [Fact]
    public async Task JobLoop_IdleBudgetExhausted_ExitsAsToday()
    {
        // A Redis that STAYS gone exhausts the same idle budget the silence of
        // an idle server does — no new config, no infinite retry.
        var bus = new Mock<IRedisJobBus>();
        var executor = new Mock<IStepExecutor>();
        bus.Setup(b => b.WaitForStepAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RedisTimeoutException("Timeout awaiting response", CommandStatus.Unknown));

        var loop = new JobLoop(bus.Object, executor.Object, NullStepInFlightMarker.Instance, NullLogger<JobLoop>.Instance);
        var exit = await loop.RunAsync("job-1", CancellationToken.None);

        exit.Should().Be(JobLoop.ExitIdleTimeout);
        bus.Verify(b => b.WaitForStepAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Exactly(JobLoop.MaxIdleCycles), "every transient counts against the same idle budget");
    }

    [Fact]
    public async Task JobLoop_NonTransientException_StillFatal()
    {
        var bus = new Mock<IRedisJobBus>();
        var executor = new Mock<IStepExecutor>();
        bus.Setup(b => b.WaitForStepAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("wire schema drift"));

        var loop = new JobLoop(bus.Object, executor.Object, NullStepInFlightMarker.Instance, NullLogger<JobLoop>.Instance);
        var act = () => loop.RunAsync("job-1", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static Step MakeRunStep(string command, params string[] args) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, command, args, "/", null, 10);
}
