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

    // p0357: the injected python payload's bin/ is prepended to every step's PATH
    // so `python3` resolves in any toolchain image; explicit step PATH wins.
    [Fact]
    public void ApplyPythonPath_PrependsBinDir()
    {
        var info = new System.Diagnostics.ProcessStartInfo();
        info.Environment["PATH"] = "/usr/bin:/bin";

        ProcessRunner.ApplyPythonPath(info, "/shared/python/bin", stepEnv: null);

        info.Environment["PATH"].Should().Be("/shared/python/bin:/usr/bin:/bin");
    }

    [Fact]
    public void ApplyPythonPath_NoPayload_LeavesPathUntouched()
    {
        var info = new System.Diagnostics.ProcessStartInfo();
        info.Environment["PATH"] = "/usr/bin";

        ProcessRunner.ApplyPythonPath(info, pythonBinDir: null, stepEnv: null);

        info.Environment["PATH"].Should().Be("/usr/bin");
    }

    [Fact]
    public void ApplyPythonPath_StepSetsPathExplicitly_StepWins()
    {
        var info = new System.Diagnostics.ProcessStartInfo();
        var stepEnv = new Dictionary<string, string> { ["PATH"] = "/custom" };
        info.Environment["PATH"] = "/custom";

        ProcessRunner.ApplyPythonPath(info, "/shared/python/bin", stepEnv);

        info.Environment["PATH"].Should().Be("/custom", "an explicit step PATH is never overridden");
    }

    private static Step MakeStep(string command, string[] args, string workingDirectory,
        IReadOnlyDictionary<string, string>? env = null, int timeoutSeconds = 10) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            command, args, workingDirectory, env, timeoutSeconds);
}
