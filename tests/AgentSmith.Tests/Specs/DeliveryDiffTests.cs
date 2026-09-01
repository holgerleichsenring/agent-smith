using AgentSmith.Application.Services;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-01-b467: a committed delivery is not an empty one.
/// <para>
/// The bundled demo produced the intended fix, committed it and then failed two of three
/// acceptance criteria with "no files were changed". The phase commits its work before the
/// gate reads it, so on a branch that IS the base — a local repository with no remote — the
/// base rungs saw nothing and the HEAD rung means uncommitted work only. Every rung compared
/// the delivery against itself.
/// </para>
/// </summary>
public sealed class DeliveryDiffTests
{
    private const string RunId = "run-b467";
    private const string FirstRunCommit = "1111111111111111111111111111111111111111";
    private const string LaterRunCommit = "2222222222222222222222222222222222222222";
    private const string Committed = "diff --git a/src/Fix.cs b/src/Fix.cs\n+++ b/src/Fix.cs\n+fixed\n";
    private const string Uncommitted = "diff --git a/src/Draft.cs b/src/Draft.cs\n+++ b/src/Draft.cs\n+draft\n";

    /// <summary>
    /// Answers like a repository: it says whether it has a remote base, which of the run's
    /// own commits its history carries, and what each comparison yields.
    /// </summary>
    private sealed class GitSandbox(bool hasRemote, bool runCommitted, string headDiff = "") : ISandbox
    {
        public string JobId => "demo";
        public List<IReadOnlyList<string>> Ran { get; } = [];

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            var args = step.Args ?? [];
            Ran.Add(args);
            var (exit, output) = Answer(args);
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, exit, false, 0.1, null, output));
        }

        private (int Exit, string Output) Answer(IReadOnlyList<string> args)
        {
            if (args.Contains("symbolic-ref"))
                return hasRemote ? (0, "origin/main") : (128, "fatal: ref refs/remotes/origin/HEAD is not a symbolic ref");
            if (args[0] == "log")
                // git log lists newest first; a run that committed twice carries both.
                return runCommitted ? (0, $"{LaterRunCommit}\n{FirstRunCommit}\n") : (0, string.Empty);
            return args[^1] switch
            {
                "origin/main" => (0, Committed),
                $"{FirstRunCommit}^" => (0, Committed),
                "HEAD" => (0, headDiff),
                _ => (128, "fatal: bad revision"),
            };
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static DeliveryDiff Diff() => TestHelpers.TestGit.Delivery;

    [Fact]
    public async Task DeliveryDiff_WorkCommittedOntoTheBaseBranch_IsSeen()
    {
        var sandbox = new GitSandbox(hasRemote: false, runCommitted: true);

        var result = await Diff().ForBranchAsync(sandbox, RunId, CancellationToken.None);

        result.Failed.Should().BeFalse();
        result.Text.Should().Be(Committed, "the run committed its work before the gate read the branch");
        result.BaseRef.Should().Be($"{FirstRunCommit}^", "the run started at the parent of its FIRST commit");
        result.Basis.Should().Contain("where this run started");
    }

    [Fact]
    public async Task DeliveryDiff_NothingCommittedYet_StillFallsBackToHead()
    {
        var sandbox = new GitSandbox(hasRemote: false, runCommitted: false, headDiff: Uncommitted);

        var result = await Diff().ForBranchAsync(sandbox, RunId, CancellationToken.None);

        result.Failed.Should().BeFalse();
        result.Text.Should().Be(Uncommitted);
        result.Basis.Should().Contain("HEAD");
        result.BaseRef.Should().BeNull("HEAD is the branch, so it names no base to search");
    }

    [Fact]
    public async Task DeliveryDiff_ARemoteBaseExists_IsUnchanged()
    {
        var sandbox = new GitSandbox(hasRemote: true, runCommitted: true);

        var result = await Diff().ForBranchAsync(sandbox, RunId, CancellationToken.None);

        result.BaseRef.Should().Be("origin/main");
        result.Basis.Should().Be("against origin/main");
        result.Text.Should().Be(Committed);
        sandbox.Ran.Where(a => a[0] == "diff").Should().ContainSingle()
            .Which.Should().NotContain($"{FirstRunCommit}^",
                "a repository with a real base is diffed against it, exactly as before");
    }

    [Fact]
    public async Task DeliveryDiff_ARunWithNoId_LeavesTheRunStartRungOut()
    {
        var sandbox = new GitSandbox(hasRemote: false, runCommitted: true, headDiff: Uncommitted);

        var result = await Diff().ForBranchAsync(sandbox, runId: null, CancellationToken.None);

        result.Text.Should().Be(Uncommitted);
        sandbox.Ran.Should().NotContain(a => a[0] == "log", "there is no run to look for");
    }
}
