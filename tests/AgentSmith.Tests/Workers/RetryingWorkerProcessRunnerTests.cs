using AgentSmith.Infrastructure.Services.Workers;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Workers;

/// <summary>
/// p0419: a dead worker process is not an answer. Run c96d had two phases verified green
/// — build and test, both repositories — when one `claude -p` exited 1 after 2.8s, and
/// the whole ticket was recorded FAILED with 45 minutes of correct work thrown away.
/// </summary>
public sealed class RetryingWorkerProcessRunnerTests
{
    private static readonly ExternalWorkerCliOptions Options =
        new("claude", ["-p"], TimeSpan.FromMinutes(5), "/tmp")
        { RetryPause = TimeSpan.FromMilliseconds(1) };

    private static RetryingWorkerProcessRunner Wrap(IWorkerProcessRunner inner) =>
        new(inner, NullLogger<RetryingWorkerProcessRunner>.Instance);

    [Fact]
    public async Task ProcessThatDies_IsAskedAgain()
    {
        var inner = new ScriptedWorkerProcessRunner()
            .EnqueueRaw(stdout: string.Empty, exitCode: 1)
            .EnqueueText("recovered");

        var result = await Wrap(inner).RunAsync("go", Options, CancellationToken.None);

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Contain("recovered");
        inner.Prompts.Should().HaveCount(2, "the second attempt asks the same question again");
    }

    [Fact]
    public async Task ProcessThatAnswered_IsNeverAskedTwice()
    {
        var inner = new ScriptedWorkerProcessRunner().EnqueueText("first and only");

        await Wrap(inner).RunAsync("go", Options, CancellationToken.None);

        inner.Prompts.Should().ContainSingle(
            "re-asking a worker that answered would double-spend the model and could "
            + "double-apply a tool call it already returned");
    }

    [Fact]
    public async Task ProcessThatKeepsDying_GivesUpAndHandsBackTheFailure()
    {
        var inner = new ScriptedWorkerProcessRunner()
            .EnqueueRaw(stdout: "Prompt is too long", exitCode: 1)
            .EnqueueRaw(stdout: "Prompt is too long", exitCode: 1)
            .EnqueueRaw(stdout: "Prompt is too long", exitCode: 1)
            .EnqueueText("never reached");

        var result = await Wrap(inner).RunAsync("go", Options, CancellationToken.None);

        result.ExitCode.Should().Be(1);
        inner.Prompts.Should().HaveCount(3, "a persistent failure is a failure, not a loop");
    }

    [Fact]
    public async Task Timeout_IsRetried_LikeAnyOtherDeadProcess()
    {
        var inner = new ScriptedWorkerProcessRunner()
            .EnqueueRaw(stdout: string.Empty, exitCode: -1, timedOut: true)
            .EnqueueText("recovered");

        var result = await Wrap(inner).RunAsync("go", Options, CancellationToken.None);

        result.TimedOut.Should().BeFalse();
        inner.Prompts.Should().HaveCount(2);
    }
}
