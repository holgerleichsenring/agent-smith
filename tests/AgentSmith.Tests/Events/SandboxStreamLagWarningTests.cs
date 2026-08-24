using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Tests.Events;

/// <summary>
/// p0491: a run step that streamed fewer characters than its result body carried is this
/// defect's signature — the drawer is showing an output the command did not stop at. p0423
/// already publishes both numbers on the result; the operator saw "OutputChars = 0,
/// DeliveredChars = 27,355" twice and read it as an unreliable field. It says so out loud now.
/// </summary>
public sealed class SandboxStreamLagWarningTests
{
    private const string RunId = "2026-08-20T17-47-19-9d83";

    [Fact]
    public async Task RunStepAsync_StreamedLessThanTheBody_WarnsAboutTheLag()
    {
        var logger = new CapturingLogger();
        var projector = Projector(streamed: ["./Sample.Distribution.Server.csproj"],
            body: new string('x', 27_355), logger: logger);

        await projector.RunStepAsync(RunStep(), progress: null, CancellationToken.None);

        logger.Warnings.Should().ContainSingle()
            .Which.Should().Contain("fell behind").And.Contain("27355");
    }

    [Fact]
    public async Task RunStepAsync_StreamKeptUp_SaysNothing()
    {
        var logger = new CapturingLogger();
        var projector = Projector(streamed: ["one", "two"], body: "one\ntwo\n", logger: logger);

        await projector.RunStepAsync(RunStep(), progress: null, CancellationToken.None);

        logger.Warnings.Should().BeEmpty("a stream that kept up is the normal case");
    }

    /// <summary>read_file and its kin never stream; only a run step's two numbers compare.</summary>
    [Fact]
    public async Task RunStepAsync_AFileRead_IsNotAStreamThatFellBehind()
    {
        var logger = new CapturingLogger();
        var projector = Projector(streamed: [], body: new string('x', 4_000), logger: logger);
        var read = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ReadFile,
            TimeoutSeconds: 30, Path: "Sample.Distribution.Server/Program.cs");

        await projector.RunStepAsync(read, progress: null, CancellationToken.None);

        logger.Warnings.Should().BeEmpty();
    }

    private static SandboxEventProjector Projector(
        IReadOnlyList<string> streamed, string? body, ILogger logger) =>
        new(new BodyOnlySandbox(streamed, body), new RecordingEventPublisher(),
            new ScopedRunContext(RunId), "default", logger);

    private static Step RunStep() =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "/bin/sh", Args: ["-c", "pwd; ls -la"], TimeoutSeconds: 60);

    private sealed class BodyOnlySandbox(IReadOnlyList<string> streamed, string? body) : ISandbox
    {
        public string JobId => "job-p0491";

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            foreach (var line in streamed)
                progress?.Report(new StepEvent(StepEvent.CurrentSchemaVersion, step.StepId,
                    StepEventKind.Stdout, line, DateTimeOffset.UtcNow));
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 0, TimedOut: false,
                DurationSeconds: 0.26, ErrorMessage: null, OutputContent: body));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ScopedRunContext(string runId) : IRunContextAccessor
    {
        public string? CurrentRunId => runId;
        public CallScope? CurrentCallScope => null;
        public int? CurrentStepIndex => null;
        public string? CurrentPhaseId => null;
        public IDisposable BeginScope(string id) => new NoOpScope();
        public IDisposable BeginStepScope(int stepIndex, string? phaseId = null) => new NoOpScope();
        public IDisposable BeginCallScope(string role, string phase, string? repoName = null) => new NoOpScope();
        private sealed class NoOpScope : IDisposable { public void Dispose() { } }
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Warnings { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) Warnings.Add(formatter(state, exception));
        }
    }
}
