using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// p0495: the ceiling a run_command may ask for is the operator's configured step cap.
/// It used to be a private const 600 raised only by a HIGHER run-command default — a bound
/// computed from something other than the bounding thing. Measured live: a solution-wide
/// test command was killed at 600.5s while the configured cap read 900.
/// </summary>
public sealed class SandboxStepRunnerTimeoutTests
{
    [Fact]
    public async Task SandboxStepRunner_RequestedTimeoutAboveTheCap_IsClampedToTheCap()
    {
        var sandbox = new StepCapturingSandbox();
        var sut = new SandboxStepRunner(sandbox, new RunCommandTimeout(300, stepTimeoutCapSeconds: 900));

        await sut.RunAsync("dotnet test", timeoutSeconds: 5400, CancellationToken.None);

        sandbox.LastStep!.TimeoutSeconds.Should().Be(900);
    }

    [Fact]
    public async Task SandboxStepRunner_RequestedTimeoutBelowTheCap_IsHonoured()
    {
        var sandbox = new StepCapturingSandbox();
        var sut = new SandboxStepRunner(sandbox, new RunCommandTimeout(300, stepTimeoutCapSeconds: 900));

        await sut.RunAsync("dotnet test", timeoutSeconds: 840, CancellationToken.None);

        sandbox.LastStep!.TimeoutSeconds.Should().Be(840,
            "the old ceiling of 600 killed exactly this command at 600.5s");
    }

    [Fact]
    public async Task SandboxStepRunner_NoRequestedTimeout_UsesTheConfiguredDefault()
    {
        var sandbox = new StepCapturingSandbox();
        var sut = new SandboxStepRunner(sandbox, new RunCommandTimeout(300, stepTimeoutCapSeconds: 900));

        await sut.RunAsync("dotnet build", timeoutSeconds: null, CancellationToken.None);

        sandbox.LastStep!.TimeoutSeconds.Should().Be(300);
    }

    [Fact]
    public async Task SandboxStepRunner_RaisingTheCap_RaisesWhatACommandMayAskFor()
    {
        var atNineHundred = new StepCapturingSandbox();
        var atEighteenHundred = new StepCapturingSandbox();

        await new SandboxStepRunner(atNineHundred, new RunCommandTimeout(300, 900))
            .RunAsync("dotnet test", 1800, CancellationToken.None);
        await new SandboxStepRunner(atEighteenHundred, new RunCommandTimeout(300, 1800))
            .RunAsync("dotnet test", 1800, CancellationToken.None);

        atNineHundred.LastStep!.TimeoutSeconds.Should().Be(900);
        atEighteenHundred.LastStep!.TimeoutSeconds.Should().Be(1800,
            "raising the cap is how an operator lets a command run longer");
    }

    private sealed class StepCapturingSandbox : ISandbox
    {
        public Step? LastStep { get; private set; }

        public string JobId => "capturing-job";

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            LastStep = step;
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 0, TimedOut: false,
                DurationSeconds: 0.1, ErrorMessage: null, OutputContent: string.Empty));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
