using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Tools;

public sealed class FilesystemToolHostRunCommandTests
{
    [Fact]
    public async Task RunCommand_ReturnsLabeledSections_WithStdoutAndStderrSeparated()
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Callback<Step, IProgress<StepEvent>?, CancellationToken>((_, progress, _) =>
            {
                progress!.Report(new StepEvent(1, Guid.NewGuid(), StepEventKind.Stdout, "stdout line", DateTimeOffset.UtcNow));
                progress.Report(new StepEvent(1, Guid.NewGuid(), StepEventKind.Stderr, "stderr line", DateTimeOffset.UtcNow));
            })
            .ReturnsAsync(new StepResult(1, Guid.NewGuid(), 0, false, 0.1, null));
        var host = new FilesystemToolHost(sandbox.Object);

        var result = await host.RunCommand("echo test");

        result.Should().Contain("exit_code: 0");
        result.Should().Contain("elapsed_ms:");
        result.Should().Contain("truncated: false");
        result.Should().Contain("stdout:\nstdout line");
        result.Should().Contain("stderr:\nstderr line");
    }

    [Fact]
    public async Task RunCommand_TimeoutOverride_PassedThroughClampedTo600s()
    {
        var sandbox = new Mock<ISandbox>();
        Step? captured = null;
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Callback<Step, IProgress<StepEvent>?, CancellationToken>((s, _, _) => captured = s)
            .ReturnsAsync(new StepResult(1, Guid.NewGuid(), 0, false, 0.1, null));
        var host = new FilesystemToolHost(sandbox.Object);

        await host.RunCommand("dotnet build", timeout_seconds: 300);
        captured!.TimeoutSeconds.Should().Be(300);

        await host.RunCommand("dotnet build", timeout_seconds: 9999);
        captured!.TimeoutSeconds.Should().Be(600);
    }

    [Fact]
    public async Task RunCommand_DefaultTimeout_Is60Seconds()
    {
        var sandbox = new Mock<ISandbox>();
        Step? captured = null;
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Callback<Step, IProgress<StepEvent>?, CancellationToken>((s, _, _) => captured = s)
            .ReturnsAsync(new StepResult(1, Guid.NewGuid(), 0, false, 0.1, null));
        var host = new FilesystemToolHost(sandbox.Object);

        await host.RunCommand("echo");
        captured!.TimeoutSeconds.Should().Be(60);
    }

    [Fact]
    public async Task RunCommand_ConfiguredDefaultTimeout_UsedWhenAgentGivesNone()
    {
        // p0230: the run_command default is configurable (per-project ?? global
        // sandbox.run_command_timeout_seconds). A project that needs longer builds
        // sets it higher; the hard-coded 60s no longer kills dotnet/npm restore.
        var sandbox = new Mock<ISandbox>();
        Step? captured = null;
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Callback<Step, IProgress<StepEvent>?, CancellationToken>((s, _, _) => captured = s)
            .ReturnsAsync(new StepResult(1, Guid.NewGuid(), 0, false, 0.1, null));
        var host = new FilesystemToolHost(sandbox.Object, runCommandTimeoutSeconds: 300);

        await host.RunCommand("dotnet build");
        captured!.TimeoutSeconds.Should().Be(300);

        // an explicit agent value can still go up to (at least) the configured default
        await host.RunCommand("dotnet build", timeout_seconds: 300);
        captured!.TimeoutSeconds.Should().Be(300);
    }

    [Fact]
    public async Task RunCommand_TimedOutResult_SetsTimedOutFlag()
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepResult(1, Guid.NewGuid(), -1, true, 60, "step timed out"));
        var host = new FilesystemToolHost(sandbox.Object);

        var result = await host.RunCommand("sleep 9999");

        result.Should().Contain("timed_out: true");
    }
}
