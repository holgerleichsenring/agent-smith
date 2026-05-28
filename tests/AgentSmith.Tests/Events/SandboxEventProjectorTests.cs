using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;

namespace AgentSmith.Tests.Events;

/// <summary>
/// p0169e seam-test (counterpart to the EventSequenceCompletenessTests
/// Theory): a StepEvent feed flowing through SandboxEventProjector MUST
/// produce SandboxCommand on entry, one SandboxOutput per stdout/stderr
/// StepEvent, and SandboxResult on completion. Counts as the silent-
/// publisher gate for the projector — if any of the three event types
/// stops surfacing, this test goes red.
/// </summary>
public sealed class SandboxEventProjectorTests
{
    private const string RunId = "2026-05-27T11-00-00-bbbb";
    private const string Repo = "default";

    [Fact]
    public async Task RunStepAsync_StepEventStdoutAndStderr_EmitsCommandOutputAndResult()
    {
        var recorder = new RecordingEventPublisher();
        var inner = new ScriptedSandbox(new[]
        {
            MakeEvent(StepEventKind.Stdout, "compiling..."),
            MakeEvent(StepEventKind.Stderr, "warning: foo"),
            MakeEvent(StepEventKind.Stdout, "done"),
        });
        var projector = new SandboxEventProjector(
            inner, recorder, new ScopedRunContext(RunId), Repo);

        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(),
            StepKind.Run, Command: "dotnet", Args: new[] { "build" }, TimeoutSeconds: 60);
        await projector.RunStepAsync(step, progress: null, CancellationToken.None);

        recorder.Types.Should().Contain(EventType.SandboxCommand);
        recorder.Types.Should().Contain(EventType.SandboxOutput);
        recorder.Types.Should().Contain(EventType.SandboxResult);
    }

    [Fact]
    public async Task RunStepAsync_PreservesStdoutAndStderrPerLine_WithBatchSeq()
    {
        var recorder = new RecordingEventPublisher();
        var inner = new ScriptedSandbox(new[]
        {
            MakeEvent(StepEventKind.Stdout, "line-1"),
            MakeEvent(StepEventKind.Stdout, "line-2"),
            MakeEvent(StepEventKind.Stderr, "warn"),
        });
        var projector = new SandboxEventProjector(
            inner, recorder, new ScopedRunContext(RunId), Repo);

        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(),
            StepKind.Run, Command: "echo", TimeoutSeconds: 5);
        await projector.RunStepAsync(step, progress: null, CancellationToken.None);

        var outputs = recorder.Events.OfType<SandboxOutputEvent>().ToList();
        outputs.Should().HaveCount(3);
        outputs.Select(o => o.Line).Should().ContainInOrder("line-1", "line-2", "warn");
        outputs.Select(o => o.BatchSeq).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task RunStepAsync_NoRunIdInScope_SkipsAllEvents()
    {
        var recorder = new RecordingEventPublisher();
        var inner = new ScriptedSandbox(new[]
        {
            MakeEvent(StepEventKind.Stdout, "ignored"),
        });
        var projector = new SandboxEventProjector(
            inner, recorder, new ScopedRunContext(null), Repo);

        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(),
            StepKind.Run, Command: "noop", TimeoutSeconds: 5);
        await projector.RunStepAsync(step, progress: null, CancellationToken.None);

        recorder.Events.Should().BeEmpty();
    }

    private static StepEvent MakeEvent(StepEventKind kind, string line) =>
        new(StepEvent.CurrentSchemaVersion, Guid.NewGuid(), kind, line, DateTimeOffset.UtcNow);

    private sealed class ScopedRunContext(string? runId) : IRunContextAccessor
    {
        public string? CurrentRunId => runId;
        public CallScope? CurrentCallScope => null;
        public IDisposable BeginScope(string id) => new NoOpScope();
        public IDisposable BeginCallScope(string role, string phase, string? repoName = null) => new NoOpScope();
        private sealed class NoOpScope : IDisposable { public void Dispose() { } }
    }

    private sealed class ScriptedSandbox(IReadOnlyList<StepEvent> events) : ISandbox
    {
        public string JobId => "scripted-job";

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            foreach (var e in events) progress?.Report(e);
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId,
                ExitCode: 0, TimedOut: false, DurationSeconds: 0.01, ErrorMessage: null, OutputContent: null));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
