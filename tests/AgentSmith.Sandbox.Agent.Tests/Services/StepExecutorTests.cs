using AgentSmith.Sandbox.Wire;
using AgentSmith.Sandbox.Agent.Services;
using AgentSmith.Sandbox.Agent.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Sandbox.Agent.Tests.Services;

public class StepExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_RunStep_ReturnsResultWithExitCode()
    {
        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.RunAsync(It.IsAny<Step>(), It.IsAny<Action<StepEventKind, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessOutcome(0, false, null));
        var executor = new StepExecutor(runner.Object, new FileStepHandler(NullLogger<FileStepHandler>.Instance), NullLogger<StepExecutor>.Instance);
        var batches = new List<IReadOnlyList<StepEvent>>();

        var result = await executor.ExecuteAsync(
            MakeRunStep("echo", "hello"),
            batch => { batches.Add(batch); return Task.CompletedTask; },
            CancellationToken.None);

        result.ExitCode.Should().Be(0);
        result.TimedOut.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_RunStep_PushesStartedAndCompletedEvents()
    {
        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.RunAsync(It.IsAny<Step>(), It.IsAny<Action<StepEventKind, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessOutcome(0, false, null));
        var executor = new StepExecutor(runner.Object, new FileStepHandler(NullLogger<FileStepHandler>.Instance), NullLogger<StepExecutor>.Instance);
        var events = new List<StepEvent>();

        await executor.ExecuteAsync(MakeRunStep("echo", "hi"),
            batch => { events.AddRange(batch); return Task.CompletedTask; },
            CancellationToken.None);

        events.Select(e => e.Kind).Should().Contain(new[] { StepEventKind.Started, StepEventKind.Completed });
    }

    [Fact]
    public async Task ExecuteAsync_TimedOutOutcome_PropagatesIntoResult()
    {
        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.RunAsync(It.IsAny<Step>(), It.IsAny<Action<StepEventKind, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessOutcome(-1, true, "timed out"));
        var executor = new StepExecutor(runner.Object, new FileStepHandler(NullLogger<FileStepHandler>.Instance), NullLogger<StepExecutor>.Instance);

        var result = await executor.ExecuteAsync(MakeRunStep("sleep", "5"),
            _ => Task.CompletedTask, CancellationToken.None);

        result.TimedOut.Should().BeTrue();
        result.ErrorMessage.Should().Be("timed out");
    }

    [Fact]
    public async Task ExecuteAsync_ShutdownStep_ThrowsArgumentException()
    {
        var runner = new Mock<IProcessRunner>();
        var executor = new StepExecutor(runner.Object, new FileStepHandler(NullLogger<FileStepHandler>.Instance), NullLogger<StepExecutor>.Instance);

        var act = () => executor.ExecuteAsync(Step.Shutdown(Guid.NewGuid()),
            _ => Task.CompletedTask, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ExecuteAsync_RealProcessThroughBatcher_FlushesEventsForVerboseOutput()
    {
        var executor = new StepExecutor(new ProcessRunner(),
            new FileStepHandler(NullLogger<FileStepHandler>.Instance),
            NullLogger<StepExecutor>.Instance);
        var allEvents = new ConcurrentEventCollector();
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            "/bin/sh", new[] { "-c", "for i in $(seq 1 60); do echo line$i; done" },
            "/", null, 10);

        await executor.ExecuteAsync(step, allEvents.Append, CancellationToken.None);

        allEvents.StdoutLines().Should().HaveCount(60);
    }

    private static Step MakeRunStep(string command, params string[] args) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, command, args, "/", null, 10);
}
