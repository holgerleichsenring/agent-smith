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
        new GitBranchPusher(), NullLogger<SandboxGitOperations>.Instance, new SandboxFileReaderFactory(),
        new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance));

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


    /// <summary>
    /// p0422: run 16 lost its spec commit to "! [rejected] (stale info)" — the branch had
    /// been deleted since this working copy last fetched, so --force-with-lease was
    /// protecting a remote state that no longer existed. A lease that can never be
    /// satisfied protects nothing; it just loses the commit.
    /// </summary>
    [Fact]
    public async Task PushRejectedAsStale_FetchesOnce_AndPushesAgain()
    {
        var sandbox = new StalePushSandbox();

        await Ops().CommitAndPushStagedAsync(
            sandbox, "agent-smith/1", "spec", Contracts.Models.Configuration.RepoType.AzureDevOps,
            CancellationToken.None);

        sandbox.RanSteps.Should().Contain(s => s.Args!.Contains("fetch"),
            "the lease is refreshed against what the remote actually is");
        sandbox.RanSteps.Count(s => s.Args!.Contains("push")).Should().Be(2);
    }

    /// <summary>Rejects the first push as stale, accepts everything else.</summary>
    private sealed class StalePushSandbox : ISandbox
    {
        private int pushes;

        public string JobId => "stale-push";

        public List<Step> RanSteps { get; } = [];

        public Task<StepResult> RunStepAsync(
            Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
        {
            RanSteps.Add(step);
            var stale = step.Args?.Contains("push") == true && pushes++ == 0;
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId,
                stale ? 1 : 0, false, 0.1,
                stale ? "! [rejected]        HEAD -> agent-smith/1 (stale info)" : null));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
