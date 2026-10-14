using AgentSmith.Application.Services.Preflight.Run;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Preflight.Run;

/// <summary>
/// p0428: a ticket branch carrying an earlier run's commits is REPORTED. p0112 and p0360
/// create exactly that state on purpose, so refusing the run would refuse the framework's
/// own resume. 2026-10-13-3890c: only commits beyond the base count.
/// </summary>
public sealed class BranchStateCheckTests
{
    private static BranchStateCheck Check() => new(
        new SandboxBaseLadder(new SandboxBaseBranch(NullLogger<SandboxBaseBranch>.Instance), NullLogger<SandboxBaseLadder>.Instance),
        NullLogger<BranchStateCheck>.Instance);

    [Fact]
    public async Task BranchState_FrameworkCommitsBeyondBase_AreCounted()
    {
        var sandbox = new GitScriptSandbox("origin/develop",
            string.Join("\n", "commit a1", SandboxGitIdentity.Email, "commit b2", SandboxGitIdentity.Email, "commit c3", "someone@example.com"));

        var finding = await Check().RunAsync(PipelineWith(sandbox), CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Warn);
        finding.Message.Should().Contain("api: 2").And.Contain("agentsmith/4242").And.Contain("beyond its base");
        finding.Message.Should().NotContain("skipped");
        sandbox.RevList!.Args.Should().ContainInOrder("rev-list", "--no-merges", "--format=%ae", "HEAD", "--not", "origin/develop");
    }

    [Fact]
    public async Task BranchState_FrameworkCommitsOnlyOnBase_ReportNothing()
    {
        var sandbox = new GitScriptSandbox("origin/develop", revList: string.Empty);

        var finding = await Check().RunAsync(PipelineWith(sandbox), CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Pass);
    }

    [Fact]
    public async Task BranchState_NoBase_ReportsNothing()
    {
        var sandbox = new GitScriptSandbox(originHead: null, revList: SandboxGitIdentity.Email);

        var finding = await Check().RunAsync(PipelineWith(sandbox), CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Pass);
        sandbox.RevList.Should().BeNull("with no base there is nothing to count against");
    }

    [Fact]
    public async Task AHistoryThatCannotBeRead_Passes()
    {
        var finding = await Check()
            .RunAsync(PipelineWith(new ScriptedSandbox("fatal: not a git repository", 128)), CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Pass);
    }

    [Fact]
    public void CountAuthored_CountsOnlyTheFrameworkIdentity()
    {
        BranchStateCheck.CountAuthored($"commit x\n{SandboxGitIdentity.Email}\nhuman@example.com\n")
            .Should().Be(1);
        BranchStateCheck.CountAuthored(string.Empty).Should().Be(0);
    }

    private static PipelineContext PipelineWith(ISandbox sandbox)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.CheckoutBranch, "agentsmith/4242");
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { ["api"] = sandbox });
        return pipeline;
    }

    /// <summary>Answers origin/HEAD and the rev-list by command, so the base ladder runs for real.</summary>
    private sealed class GitScriptSandbox(string? originHead, string revList) : ISandbox
    {
        public string JobId => "git-script";

        public Step? RevList { get; private set; }

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
        {
            var args = step.Args ?? [];
            if (args.Contains("symbolic-ref"))
                return Answer(step, originHead is null ? 128 : 0, originHead ?? string.Empty);
            if (args.Contains("rev-list"))
            {
                RevList = step;
                return Answer(step, 0, revList);
            }
            return Answer(step, 1, string.Empty);
        }

        private static Task<StepResult> Answer(Step step, int exit, string output) =>
            Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, exit,
                TimedOut: false, DurationSeconds: 0.01, ErrorMessage: null, OutputContent: output));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
