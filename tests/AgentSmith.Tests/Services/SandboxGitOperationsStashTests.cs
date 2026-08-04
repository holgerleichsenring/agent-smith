using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

// p0400: baseline mode for ships_code:false phases parks the phase's working
// changes via stash. "No local changes to save" exits 0 and must report false —
// a pop after it would corrupt someone else's stash entry.
public sealed class SandboxGitOperationsStashTests
{
    private static SandboxGitOperations Ops() => new(
        NullLogger<SandboxGitOperations>.Instance, new SandboxFileReaderFactory());

    [Fact]
    public async Task StashWorkingChanges_EntryCreated_ReturnsTrue()
    {
        var sandbox = new ScriptedSandbox("Saved working directory and index state WIP on main");

        (await Ops().StashWorkingChangesAsync(sandbox, CancellationToken.None)).Should().BeTrue();
        sandbox.RanSteps.Should().Contain(s =>
            s.Command == "git" && s.Args!.Contains("stash") && s.Args!.Contains("push"));
    }

    [Fact]
    public async Task StashWorkingChanges_NothingToStash_ReturnsFalse()
    {
        var sandbox = new ScriptedSandbox("No local changes to save");

        (await Ops().StashWorkingChangesAsync(sandbox, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task RestoreStashedChanges_IssuesStashPop()
    {
        var sandbox = new ScriptedSandbox(string.Empty);

        await Ops().RestoreStashedChangesAsync(sandbox, CancellationToken.None);

        sandbox.RanSteps.Should().Contain(s =>
            s.Command == "git" && s.Args!.Contains("stash") && s.Args!.Contains("pop"));
    }

    private sealed class ScriptedSandbox(string stashOutput) : ISandbox
    {
        public string JobId => "stash-test";
        public List<Step> RanSteps { get; } = new();

        public Task<StepResult> RunStepAsync(
            Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
        {
            RanSteps.Add(step);
            var output = step.Args?.Contains("stash") == true ? stashOutput : string.Empty;
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 0,
                TimedOut: false, DurationSeconds: 0.01, ErrorMessage: null, OutputContent: output));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
