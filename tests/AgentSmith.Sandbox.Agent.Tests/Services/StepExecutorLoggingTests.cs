using AgentSmith.Sandbox.Agent.Services;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Sandbox.Agent.Tests.Services;

public sealed class StepExecutorLoggingTests
{
    [Fact]
    public async Task RunStep_LogsActualShellCommand_NotBinSh()
    {
        var (executor, logger) = Build();
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "/bin/sh", Args: new[] { "-c", "echo hello" });

        await executor.ExecuteAsync(step, _ => Task.CompletedTask, CancellationToken.None);

        logger.Lines.Should().ContainSingle(l => l.Level == LogLevel.Information && l.Message.Contains("`echo hello`"));
        logger.Lines.Should().NotContain(l => l.Level == LogLevel.Information && l.Message.Contains("`/bin/sh`"));
    }

    [Fact]
    public async Task RunStep_EmitsOneInfoLine_NotTwo()
    {
        var (executor, logger) = Build();
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "/bin/sh", Args: new[] { "-c", "echo hi" });

        await executor.ExecuteAsync(step, _ => Task.CompletedTask, CancellationToken.None);

        logger.Lines.Count(l => l.Level == LogLevel.Information).Should().Be(1);
    }

    [Fact]
    public async Task RunStep_LogLineIncludesShortStepIdAndElapsedMs()
    {
        var (executor, logger) = Build();
        var stepId = Guid.NewGuid();
        var step = new Step(Step.CurrentSchemaVersion, stepId, StepKind.Run,
            Command: "/bin/sh", Args: new[] { "-c", "true" });

        await executor.ExecuteAsync(step, _ => Task.CompletedTask, CancellationToken.None);

        var line = logger.Lines.Single(l => l.Level == LogLevel.Information).Message;
        line.Should().Contain(stepId.ToString("N")[..8]);
        line.Should().MatchRegex(@"in \d+ms");
        line.Should().Contain("exit=0");
    }

    [Fact]
    public async Task RunStep_NonZeroExit_LogsAtWarningLevel()
    {
        var (executor, logger) = Build(exitCode: 1);
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "/bin/sh", Args: new[] { "-c", "false" });

        await executor.ExecuteAsync(step, _ => Task.CompletedTask, CancellationToken.None);

        logger.Lines.Should().Contain(l => l.Level == LogLevel.Warning && l.Message.Contains("exit=1"));
        logger.Lines.Should().NotContain(l => l.Level == LogLevel.Information && l.Message.Contains("exit="));
    }

    [Fact]
    public async Task RunStep_DirectCommand_NotShell_StillLoggedCleanly()
    {
        var (executor, logger) = Build();
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git", Args: new[] { "status" });

        await executor.ExecuteAsync(step, _ => Task.CompletedTask, CancellationToken.None);

        logger.Lines.Should().Contain(l => l.Level == LogLevel.Information && l.Message.Contains("`git status`"));
    }

    [Fact]
    public async Task RunStep_CapturesStdoutIntoOutputContent_NotStderr()
    {
        // p0258: Run steps must populate StepResult.OutputContent — not stream
        // output ONLY via progress events. SandboxGitOperations reads
        // result.OutputContent for git porcelain (`git diff --cached --name-only`
        // → staged file names). A null OutputContent made every git read look
        // empty → CommitAndPR saw hasCode=False and opened no PR even though the
        // master's edits were really in /work.
        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.RunAsync(It.IsAny<Step>(), It.IsAny<Action<StepEventKind, string>>(), It.IsAny<CancellationToken>()))
            .Returns((Step _, Action<StepEventKind, string> onLine, CancellationToken _) =>
            {
                onLine(StepEventKind.Stdout, "src/Foo.cs");
                onLine(StepEventKind.Stdout, "src/Bar.cs");
                onLine(StepEventKind.Stderr, "noise-on-stderr");
                return Task.FromResult(new ProcessOutcome(0, TimedOut: false, ErrorMessage: null));
            });
        var executor = new StepExecutor(runner.Object,
            new FileStepHandler(NullLogger<FileStepHandler>.Instance),
            new GrepStepHandler(runner.Object, NullLogger<GrepStepHandler>.Instance),
            new DirectoryTreeStepHandler(NullLogger<DirectoryTreeStepHandler>.Instance),
            NullLogger<StepExecutor>.Instance);
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git", Args: new[] { "diff", "--cached", "--name-only" });

        var result = await executor.ExecuteAsync(step, _ => Task.CompletedTask, CancellationToken.None);

        result.OutputContent.Should().NotBeNull();
        result.OutputContent.Should().Contain("src/Foo.cs").And.Contain("src/Bar.cs");
        result.OutputContent.Should().NotContain("noise-on-stderr", "only stdout is captured into OutputContent");
    }

    private static (StepExecutor Executor, RecordingLogger<StepExecutor> Logger) Build(int exitCode = 0)
    {
        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.RunAsync(It.IsAny<Step>(), It.IsAny<Action<StepEventKind, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessOutcome(exitCode, TimedOut: false, ErrorMessage: null));
        var logger = new RecordingLogger<StepExecutor>();
        var fileHandler = new FileStepHandler(NullLogger<FileStepHandler>.Instance);
        var grepHandler = new GrepStepHandler(runner.Object, NullLogger<GrepStepHandler>.Instance);
        var treeHandler = new DirectoryTreeStepHandler(NullLogger<DirectoryTreeStepHandler>.Instance);
        var executor = new StepExecutor(runner.Object, fileHandler, grepHandler, treeHandler, logger);
        return (executor, logger);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Lines { get; } = new();
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Lines.Add((logLevel, formatter(state, exception)));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
