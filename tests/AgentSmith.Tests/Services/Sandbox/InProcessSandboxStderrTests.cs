using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Sandbox;

/// <summary>
/// Regression: pre-fix every non-zero exit returned <c>ErrorMessage:null</c>,
/// so callers logged "git clone failed (exit=128): " with an empty trailing
/// message. The actual git error (auth failure, repo-not-found, ...) was
/// only emitted as a progress event and disappeared. The fix captures
/// stderr into the StepResult so callers can surface it.
/// </summary>
public sealed class InProcessSandboxStderrTests : IAsyncDisposable
{
    private readonly string _workDir;
    private readonly InProcessSandbox _sandbox;

    public InProcessSandboxStderrTests()
    {
        _workDir = Path.Combine(Path.GetTempPath(), $"agentsmith-stderr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workDir);
        _sandbox = new InProcessSandbox(
            jobId: "test", workDir: _workDir, ownsWorkDir: true,
            logger: NullLogger<InProcessSandbox>.Instance);
    }

    public async ValueTask DisposeAsync() => await _sandbox.DisposeAsync();

    [Fact]
    public async Task RunStepAsync_NonZeroExit_PropagatesStderrInErrorMessage()
    {
        var step = new Step(
            Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "/bin/sh",
            Args: ["-c", "echo something-broke 1>&2; exit 7"],
            WorkingDirectory: "/work");

        var result = await _sandbox.RunStepAsync(step, progress: null, CancellationToken.None);

        result.ExitCode.Should().Be(7);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.ErrorMessage.Should().Contain("something-broke");
    }

    [Fact]
    public async Task RunStepAsync_ZeroExit_LeavesErrorMessageNull()
    {
        var step = new Step(
            Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "/bin/sh",
            Args: ["-c", "echo not-a-real-error 1>&2; exit 0"],
            WorkingDirectory: "/work");

        var result = await _sandbox.RunStepAsync(step, progress: null, CancellationToken.None);

        result.ExitCode.Should().Be(0);
        result.ErrorMessage.Should().BeNull("zero-exit means success — stderr noise is not an error");
    }

    [Fact]
    public async Task RunStepAsync_StderrOver8K_TruncatesToBudget()
    {
        // Write 12 KiB of stderr; budget is 8 KiB.
        var step = new Step(
            Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "/bin/sh",
            Args: ["-c", "for i in $(seq 1 200); do printf 'line-%03d-padding-padding-padding-padding-padding\\n' $i 1>&2; done; exit 1"],
            WorkingDirectory: "/work");

        var result = await _sandbox.RunStepAsync(step, progress: null, CancellationToken.None);

        result.ExitCode.Should().Be(1);
        result.ErrorMessage.Should().NotBeNull();
        result.ErrorMessage!.Length.Should().BeLessThan(9_000,
            "stderr capture is bounded so a chatty subprocess can't blow the heap");
        result.ErrorMessage.Should().Contain("line-001");
    }
}
