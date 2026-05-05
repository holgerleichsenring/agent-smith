using AgentSmith.Sandbox.Wire;
using AgentSmith.Sandbox.Agent.Services;
using FluentAssertions;

namespace AgentSmith.Sandbox.Agent.Tests.Services;

public class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_EchoHello_CapturesStdoutAndExitsZero()
    {
        var lines = new List<(StepEventKind Kind, string Line)>();
        var runner = new ProcessRunner();
        var step = MakeStep("echo", new[] { "hello" }, workingDirectory: "/");

        var outcome = await runner.RunAsync(step, (k, l) => lines.Add((k, l)), CancellationToken.None);

        outcome.ExitCode.Should().Be(0);
        outcome.TimedOut.Should().BeFalse();
        lines.Should().Contain((StepEventKind.Stdout, "hello"));
    }

    [Fact]
    public async Task RunAsync_StderrOutput_IsCapturedAsStderr()
    {
        var lines = new List<(StepEventKind Kind, string Line)>();
        var runner = new ProcessRunner();
        var step = MakeStep("/bin/sh", new[] { "-c", "echo err >&2" }, workingDirectory: "/");

        await runner.RunAsync(step, (k, l) => lines.Add((k, l)), CancellationToken.None);

        lines.Should().Contain((StepEventKind.Stderr, "err"));
    }

    [Fact]
    public async Task RunAsync_LongerThanTimeout_ReportsTimedOut()
    {
        var runner = new ProcessRunner();
        var step = MakeStep("/bin/sh", new[] { "-c", "sleep 5" }, workingDirectory: "/", timeoutSeconds: 1);

        var outcome = await runner.RunAsync(step, (_, _) => { }, CancellationToken.None);

        outcome.TimedOut.Should().BeTrue();
        outcome.ExitCode.Should().Be(-1);
    }

    [Fact]
    public async Task RunAsync_WorkingDirectory_IsHonored()
    {
        var lines = new List<string>();
        var runner = new ProcessRunner();
        var step = MakeStep("/bin/sh", new[] { "-c", "pwd" }, workingDirectory: "/tmp");

        await runner.RunAsync(step, (_, l) => lines.Add(l), CancellationToken.None);

        lines.Should().Contain(l => l.EndsWith("/tmp", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_NonexistentCommand_ReturnsFailureWithoutThrowing()
    {
        var runner = new ProcessRunner();
        var step = MakeStep("/no/such/binary-xyz", Array.Empty<string>(), workingDirectory: "/");

        var outcome = await runner.RunAsync(step, (_, _) => { }, CancellationToken.None);

        outcome.ExitCode.Should().Be(-1);
        outcome.TimedOut.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("/no/such/binary-xyz");
    }

    [Fact]
    public async Task RunAsync_NonexistentWorkingDirectory_ReturnsFailureWithoutThrowing()
    {
        var runner = new ProcessRunner();
        var step = MakeStep("echo", new[] { "x" }, workingDirectory: "/no/such/dir");

        var outcome = await runner.RunAsync(step, (_, _) => { }, CancellationToken.None);

        outcome.ExitCode.Should().Be(-1);
        outcome.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task RunAsync_EnvVars_ArePassedToProcess()
    {
        var lines = new List<string>();
        var runner = new ProcessRunner();
        var step = MakeStep("/bin/sh", new[] { "-c", "echo $FOO" },
            workingDirectory: "/",
            env: new Dictionary<string, string> { ["FOO"] = "bar" });

        await runner.RunAsync(step, (_, l) => lines.Add(l), CancellationToken.None);

        lines.Should().Contain("bar");
    }

    private static Step MakeStep(string command, string[] args, string workingDirectory,
        IReadOnlyDictionary<string, string>? env = null, int timeoutSeconds = 10) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            command, args, workingDirectory, env, timeoutSeconds);
}
