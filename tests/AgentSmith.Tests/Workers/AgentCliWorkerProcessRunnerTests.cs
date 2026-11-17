using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Workers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Workers;

/// <summary>
/// p0416: the subprocess transport itself, against a real process. The prompt goes in on
/// stdin (a conversation with tool schemas exceeds any command line) and the answer comes
/// back on stdout — proven here rather than assumed, because the scripted harness runner
/// deliberately does not exercise it.
/// </summary>
public sealed class AgentCliWorkerProcessRunnerTests
{
    private static AgentCliWorkerProcessRunner NewRunner() =>
        new(NullLogger<AgentCliWorkerProcessRunner>.Instance);

    private static ExternalWorkerCliOptions Shell(string script, int timeoutSeconds = 30) =>
        new("/bin/sh", ["-c", script], TimeSpan.FromSeconds(timeoutSeconds), Path.GetTempPath());

    [Fact]
    public async Task Run_PromptGoesInOnStdin_AnswerComesBackOnStdout()
    {
        if (OperatingSystem.IsWindows()) return; // POSIX shell stand-in for the agent CLI

        var result = await NewRunner().RunAsync(
            new WorkerPrompt("", """{"call":"one"}"""), Shell("cat"), CancellationToken.None);

        result.ExitCode.Should().Be(0);
        result.TimedOut.Should().BeFalse();
        result.StandardOutput.Should().Contain("""{"call":"one"}""");
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero, "per-call latency is the real cost here");
    }

    // 2026-11-17-cb32b: the options' variables reach the process, over the inherited ones.
    [Fact]
    public async Task Run_OptionsEnvironment_ReachesTheProcess()
    {
        if (OperatingSystem.IsWindows()) return;
        var options = Shell("printf '%s' \"$CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC\"") with
        {
            Environment = new Dictionary<string, string> { ["CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC"] = "1" },
        };

        var result = await NewRunner().RunAsync(new WorkerPrompt("", "x"), options, CancellationToken.None);

        result.StandardOutput.Should().Be("1");
    }

    [Fact]
    public async Task Run_NonZeroExit_IsReportedWithItsStderr()
    {
        if (OperatingSystem.IsWindows()) return;

        var result = await NewRunner().RunAsync(
            new WorkerPrompt("", "ignored"), Shell("cat > /dev/null; echo 'auth failed' >&2; exit 3"), CancellationToken.None);

        result.ExitCode.Should().Be(3);
        result.StandardError.Should().Contain("auth failed");
    }

    [Fact]
    public async Task Run_ExceedingTheTimeout_ReportsTimedOut_AndKillsTheProcess()
    {
        if (OperatingSystem.IsWindows()) return;

        // 2026-09-22-3f7c: both sides of the duration comparison are real time on the same
        // clock — the child's own sleep, and how long the runner waited for it — so a slower
        // host scales neither. Twenty seconds was a third number, belonging to neither.
        var childSleep = TimeSpan.FromSeconds(30);
        var result = await NewRunner().RunAsync(
            new WorkerPrompt("", "ignored"), Shell($"sleep {childSleep.TotalSeconds:F0}", timeoutSeconds: 1),
            CancellationToken.None);

        result.TimedOut.Should().BeTrue();
        result.Duration.Should().BeLessThan(childSleep,
            "the runner stops at its own timeout rather than at the child's end");
    }

    [Fact]
    public async Task Run_OperatorCancel_PropagatesRatherThanLookingLikeATimeout()
    {
        if (OperatingSystem.IsWindows()) return;

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var act = async () => await NewRunner().RunAsync(new WorkerPrompt("", "ignored"), Shell("sleep 30"), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void CliOptions_ComeFromTheAgentsOwnConfig()
    {
        var agent = new AgentConfig
        {
            Type = "external_worker", Endpoint = "/opt/bin/claude", NetworkTimeoutSeconds = 1800,
        };

        var options = new ExternalWorkerCliOptionsFactory()
            .Create(agent, new ModelAssignment { Model = "sonnet" });

        options.Binary.Should().Be("/opt/bin/claude", "endpoint is where the provider lives");
        // 2026-09-01-b0d7: the default CLI is asked for its structured result.
        options.Arguments.Should().Equal("-p", "--model", "sonnet",
            "--input-format", "stream-json", "--output-format", "stream-json", "--verbose",
            "--tools", "", "--strict-mcp-config", "--no-session-persistence");
        options.InputAsBlocks.Should().BeTrue("2026-11-17-d6f5: the input goes in as content blocks");
        options.Timeout.Should().Be(TimeSpan.FromMinutes(30), "a worker takes minutes, not seconds");
        options.WorkingDirectory.Should().NotContain("agent-smith",
            "the worker answers a model call; it must not pick up the repo under change");
    }

    [Fact]
    public void CliOptions_DefaultToTheClaudeCliWhenNothingIsConfigured()
    {
        var options = new ExternalWorkerCliOptionsFactory()
            .Create(new AgentConfig { Type = "external_worker" }, new ModelAssignment());

        options.Binary.Should().Be(ExternalWorkerCliOptionsFactory.DefaultBinary);
        options.Arguments.Should().Equal("-p",
            "--input-format", "stream-json", "--output-format", "stream-json", "--verbose",
            "--tools", "", "--strict-mcp-config", "--no-session-persistence");
        options.Timeout.Should().Be(TimeSpan.FromSeconds(300));
    }
}
