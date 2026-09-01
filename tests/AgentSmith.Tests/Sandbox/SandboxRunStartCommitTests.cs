using AgentSmith.Application.Services;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-01-b467: where a run began is read from the run's OWN commits, so a commit
/// somebody else made on the same branch cannot be mistaken for the run's starting point.
/// </summary>
public sealed class SandboxRunStartCommitTests
{
    private const string RunId = "run-b467";
    private const string First = "1111111111111111111111111111111111111111";
    private const string Later = "2222222222222222222222222222222222222222";

    private sealed class ScriptedSandbox(int exitCode, string output) : ISandbox
    {
        public string JobId => "demo";
        public Step? Ran { get; private set; }

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            Ran = step;
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, exitCode, false, 0.1, null, output));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Resolve_TheRunsOldestCommit_NamesItsParentAsTheStartingPoint()
    {
        var sandbox = new ScriptedSandbox(0, $"{Later}\n{First}\n");

        var start = await TestHelpers.TestGit.RunStartCommit.ResolveAsync(
            sandbox, RunId, CancellationToken.None);

        start.Should().Be($"{First}^", "git log lists newest first, so the last line is the run's first commit");
        sandbox.Ran!.Args.Should().Contain($"--grep={RunCheckpointCommit.MessageFor(RunId)}")
            .And.Contain("--fixed-strings");
    }

    [Fact]
    public async Task Resolve_ARunThatHasCommittedNothing_NamesNoStartingPoint()
    {
        var start = await TestHelpers.TestGit.RunStartCommit.ResolveAsync(
            new ScriptedSandbox(0, string.Empty), RunId, CancellationToken.None);

        start.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_AHistoryThatCannotBeRead_NamesNoStartingPoint()
    {
        var start = await TestHelpers.TestGit.RunStartCommit.ResolveAsync(
            new ScriptedSandbox(128, "fatal: not a git repository"), RunId, CancellationToken.None);

        start.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_NoRunId_AsksTheHistoryNothing()
    {
        var sandbox = new ScriptedSandbox(0, $"{First}\n");

        var start = await TestHelpers.TestGit.RunStartCommit.ResolveAsync(
            sandbox, runId: null, CancellationToken.None);

        start.Should().BeNull();
        sandbox.Ran.Should().BeNull("a marker with no run id would match another run's commit");
    }

    [Fact]
    public void CheckpointMessage_NamesTheRun_SoNoOneElsesCommitCanMatchIt()
    {
        RunCheckpointCommit.MessageFor(RunId).Should().Be($"[checkpoint] agent-smith run {RunId}");
    }
}
