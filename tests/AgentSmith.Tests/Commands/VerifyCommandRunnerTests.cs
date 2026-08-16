using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// p0425: a declared verification command is a command LINE.
/// <para>
/// Ticket 19192 declared <c>dotnet test A.Integration.Tests &amp;&amp; dotnet test
/// B.Integration.Tests</c>. The runner tokenised it and executed argv[0] with the rest as
/// arguments, so <c>&amp;&amp;</c> reached MSBuild — "MSB1008: Only one project can be
/// specified", "Switch: &amp;&amp;". The run failed at its verification step after both
/// phases of real work had succeeded.
/// </para>
/// <para>
/// The agent's own run_command tool has always passed its text to <c>/bin/sh -c</c>. The
/// command whose exit code DECIDES the run was the only one that could not.
/// </para>
/// </summary>
public sealed class VerifyCommandRunnerTests
{
    [Fact]
    public async Task ADeclaredCommand_WithTwoInvocations_RunsBoth()
    {
        var sandbox = new RecordingSandbox();
        var runner = new VerifyCommandRunner(NullLogger<VerifyCommandRunner>.Instance);

        await runner.RunAsync(
            "server", "test", sandbox, "/work",
            "dotnet test A.Integration.Tests && dotnet test B.Integration.Tests",
            CancellationToken.None);

        sandbox.LastStep!.Args.Should().HaveCount(2);
        sandbox.LastStep.Args![1].Should()
            .Be("dotnet test A.Integration.Tests && dotnet test B.Integration.Tests",
                "the separator belongs to the shell, not to MSBuild's argument list");
    }

    [Fact]
    public async Task ADeclaredCommand_RunsThroughTheSameShellAsTheAgentsOwnTool()
    {
        var sandbox = new RecordingSandbox();
        var runner = new VerifyCommandRunner(NullLogger<VerifyCommandRunner>.Instance);

        await runner.RunAsync("server", "build", sandbox, "/work", "dotnet build", CancellationToken.None);

        sandbox.LastStep!.Command.Should().Be("/bin/sh");
        sandbox.LastStep.Args![0].Should().Be("-c");
        sandbox.LastStep.WorkingDirectory.Should().Be("/work");
    }

    [Fact]
    public async Task ABlankCommand_IsTreatedAsAbsent()
    {
        var sandbox = new RecordingSandbox();
        var runner = new VerifyCommandRunner(NullLogger<VerifyCommandRunner>.Instance);

        var outcome = await runner.RunAsync("server", "test", sandbox, "/work", "   ", CancellationToken.None);

        outcome.Skipped.Should().BeTrue();
        outcome.ExitCode.Should().Be(0);
        sandbox.LastStep.Should().BeNull("a blank declaration is not a command to run");
    }

    private sealed class RecordingSandbox : ISandbox
    {
        public Step? LastStep { get; private set; }

        public string JobId => "test";

        public Task<StepResult> RunStepAsync(
            Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
        {
            LastStep = step;
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 0, TimedOut: false,
                DurationSeconds: 0.1, ErrorMessage: null, OutputContent: "ok"));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
