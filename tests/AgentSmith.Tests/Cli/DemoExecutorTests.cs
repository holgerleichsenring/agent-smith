using AgentSmith.Cli.Services.Demo;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Cli;

/// <summary>
/// p0326: the demo's preflight gate — a broken environment (no/invalid LLM key)
/// fails with the fix hint BEFORE the pipeline leg runs, so zero pipeline
/// tokens are spent; a green preflight hands off to the runner.
/// </summary>
public sealed class DemoExecutorTests
{
    [Fact]
    public async Task DemoCommand_NoLlmKey_FailsAtPreflightNotMidRun()
    {
        var report = new PreflightReport([
            new PreflightCheckOutcome("config-schema", "config", PreflightCheckResult.Pass("ok"), 2),
            new PreflightCheckOutcome("llm-reachable", "llm",
                PreflightCheckResult.Fail("401 Unauthorized", "set the provider api key secret"), 120),
        ]);
        var runner = new RecordingDemoRunner();
        var executor = new DemoExecutor(new ScriptedRunner(report), runner);
        var output = new StringWriter();

        var exitCode = await executor.ExecuteAsync(
            new DemoInvocation("agentsmith.yml"), output, CancellationToken.None);

        exitCode.Should().Be(1);
        runner.Invoked.Should().BeFalse("a failed preflight must stop BEFORE the pipeline spends tokens");
        output.ToString().Should().Contain("set the provider api key secret", "the fix hint must be printed");
        output.ToString().Should().Contain("no tokens were spent");
    }

    [Fact]
    public async Task ExecuteAsync_PreflightGreen_HandsOffToRunner()
    {
        var report = new PreflightReport([
            new PreflightCheckOutcome("llm-reachable", "llm", PreflightCheckResult.Pass("ok"), 90),
        ]);
        var runner = new RecordingDemoRunner { ExitCode = 0 };
        var executor = new DemoExecutor(new ScriptedRunner(report), runner);

        var exitCode = await executor.ExecuteAsync(
            new DemoInvocation("agentsmith.yml"), new StringWriter(), CancellationToken.None);

        exitCode.Should().Be(0);
        runner.Invoked.Should().BeTrue();
    }

    private sealed class ScriptedRunner(PreflightReport report) : IPreflightRunner
    {
        public Task<PreflightReport> RunAsync(CancellationToken cancellationToken) =>
            Task.FromResult(report);
    }

    private sealed class RecordingDemoRunner : IDemoRunner
    {
        public bool Invoked { get; private set; }
        public int ExitCode { get; init; }

        public Task<int> RunAsync(
            DemoInvocation invocation, TextWriter output, CancellationToken cancellationToken)
        {
            Invoked = true;
            return Task.FromResult(ExitCode);
        }
    }
}
