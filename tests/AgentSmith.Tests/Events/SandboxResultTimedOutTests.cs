using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;

namespace AgentSmith.Tests.Events;

/// <summary>
/// p0495: StepResult.TimedOut exists and ProcessRunner sets it, but SandboxResultEvent had
/// no such field — so the truth was dropped at the event boundary and the dashboard fell
/// back to exit -1, printing "not run · 600.5s" for a command that ran for ten minutes.
/// Exit -1 keeps its other meaning: a cancelled run's probes legitimately never start.
/// </summary>
public sealed class SandboxResultTimedOutTests
{
    private const string RunId = "2026-08-20T09-00-00-cccc";
    private const string Repo = "default";

    [Fact]
    public async Task SandboxResult_TimedOutCommand_CarriesTheTimedOutFlag()
    {
        var recorder = new RecordingEventPublisher();
        var projector = new SandboxEventProjector(
            new ScriptedResultSandbox(exitCode: -1, timedOut: true),
            recorder, new StubRunContext(RunId), Repo);

        await projector.RunStepAsync(RunStep(900), progress: null, CancellationToken.None);

        var result = recorder.Events.OfType<SandboxResultEvent>().Single();
        result.TimedOut.Should().BeTrue();
        result.TimeoutSeconds.Should().Be(900, "the reader must be told what killed it");
    }

    [Fact]
    public async Task SandboxResult_CommandThatNeverStarted_DoesNotCarryIt()
    {
        var recorder = new RecordingEventPublisher();
        var projector = new SandboxEventProjector(
            new ScriptedResultSandbox(exitCode: -1, timedOut: false),
            recorder, new StubRunContext(RunId), Repo);

        await projector.RunStepAsync(RunStep(900), progress: null, CancellationToken.None);

        var result = recorder.Events.OfType<SandboxResultEvent>().Single();
        result.ExitCode.Should().Be(-1);
        result.TimedOut.Should().BeFalse();
    }

    private static Step RunStep(int timeoutSeconds) => new(
        Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
        Command: "/bin/sh", Args: ["-c", "dotnet test"], TimeoutSeconds: timeoutSeconds);

    private sealed class StubRunContext(string? runId) : IRunContextAccessor
    {
        public string? CurrentRunId => runId;
        public CallScope? CurrentCallScope => null;
        public IDisposable BeginScope(string id) => new NoOpScope();
        public int? CurrentStepIndex => null;
        public string? CurrentPhaseId => null;
        public IDisposable BeginStepScope(int stepIndex, string? phaseId = null) => new NoOpScope();
        public IDisposable BeginCallScope(string role, string phase, string? repoName = null) => new NoOpScope();
        private sealed class NoOpScope : IDisposable { public void Dispose() { } }
    }

    private sealed class ScriptedResultSandbox(int exitCode, bool timedOut) : ISandbox
    {
        public string JobId => "timeout-job";

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct) =>
            Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, exitCode, timedOut,
                DurationSeconds: 600.5,
                ErrorMessage: timedOut ? "timed out after 900s" : null));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
