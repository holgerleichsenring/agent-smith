using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// 2026-11-17-023fa: a green code run merges its own pull requests only when the project says on_gate_green and
/// only those CommitAndPR published as gate-passed; it merges in the project's protection mode, stops at the first
/// refusal, leaves a rung-targeted pull request open, and records and says every outcome.
/// </summary>
public sealed class CompletePullRequestsHandlerTests
{
    private readonly List<(string Url, BranchProtection Mode)> _merged = [];
    private readonly List<PullRequestOutcomeEvent> _outcomes = [];
    private readonly Dictionary<string, PullRequestCompletion> _answers = new();
    private readonly List<string> _approved = [];
    private PullRequestApproval _approval = PullRequestApproval.Approved;

    [Fact]
    public async Task Manual_MergesNothing()
    {
        var result = await RunAsync(new MergePolicyConfig(), Passed("a"));

        result.Message.Should().Contain("merge: manual");
        _merged.Should().BeEmpty();
    }

    [Fact]
    public async Task NoGatePassedPullRequests_MergesNothing_AndSaysWhy()
    {
        var result = await RunAsync(OnGreen(), passed: null);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("did not pass its gate");
        _merged.Should().BeEmpty();
    }

    [Fact]
    public async Task OnGateGreen_MergesEach_InTheProjectsMode_AndRecordsIt()
    {
        var result = await RunAsync(OnGreen(respect: true), Passed("a", "b"));

        _merged.Should().Equal(("https://x/a/pull/1", BranchProtection.Respect), ("https://x/b/pull/1", BranchProtection.Respect));
        _outcomes.Should().AllSatisfy(o => o.Status.Should().Be(PullRequestStatuses.Completed));
        result.Message.Should().Contain("a: merged").And.Contain("b: merged");
    }

    [Fact]
    public async Task FirstRefusal_StopsTheRest_AndNamesTheReason()
    {
        _answers["https://x/a/pull/1"] = PullRequestCompletion.Refused(
            "cannot bypass branch protection on GitHub: review required; grant admin or set respect_branch_protection: true");

        var result = await RunAsync(OnGreen(), Passed("a", "b"));

        _merged.Select(m => m.Url).Should().Equal("https://x/a/pull/1");
        _merged.Single().Mode.Should().Be(BranchProtection.Bypass, "respect_branch_protection defaults to false");
        result.Message.Should().Contain("a: still open — cannot bypass branch protection");
        result.Message.Should().Contain("b: not attempted");
        _outcomes.Single().Status.Should().Be(PullRequestStatuses.CompletionRefused);
    }

    [Fact]
    public async Task Armed_IsNotARefusal()
    {
        _answers["https://x/a/pull/1"] = PullRequestCompletion.Armed("waiting on the build policy");

        var result = await RunAsync(OnGreen(respect: true), Passed("a", "b"));

        _merged.Should().HaveCount(2);
        result.Message.Should().Contain("a: armed — waiting on the build policy");
        _outcomes.First().Status.Should().Be(PullRequestStatuses.CompletionArmed);
    }

    [Fact]
    public async Task RungTarget_IsLeftOpen_AndNamed()
    {
        var result = await RunAsync(OnGreen(), Passed("a"), pipeline => PullRequestTargets.Record(pipeline, "a", "feature/epic"));

        _merged.Should().BeEmpty();
        result.Message.Should().Contain("opened against 'feature/epic'");
        _outcomes.Single().Status.Should().Be(PullRequestStatuses.CompletionRefused);
    }

    // 2026-11-17-3435b
    [Fact]
    public async Task OnCiGreen_CodeRun_RecordsAwaitingCi_AndMergesNothing()
    {
        var result = await RunAsync(OnCi(), Passed("a", "b"));

        _merged.Should().BeEmpty();
        _outcomes.Should().HaveCount(2).And.AllSatisfy(o => o.Status.Should().Be(PullRequestStatuses.AwaitingCi));
        result.Message.Should().Contain("a, b wait for CI");
    }

    [Fact]
    public async Task OnCiGreen_MergeGreenRun_ApprovesInRespect_MergesAndTellsTheAttempt()
    {
        var result = await RunAsync(OnCi(respect: true), passed: null, Green("a"));

        _approved.Should().Equal("https://x/a/pull/1");
        _merged.Should().Equal(("https://x/a/pull/1", BranchProtection.Respect));
        _outcomes.Select(o => (o.RunId, o.Status)).Should().Equal(
            ("run-1", PullRequestStatuses.Completed), ("attempt-1", PullRequestStatuses.Completed));
        result.Message.Should().Contain("CI green").And.Contain("a: merged");
    }

    [Fact]
    public async Task OnCiGreen_Bypass_NeedsNoApproval()
    {
        await RunAsync(OnCi(), passed: null, Green("a"));

        _approved.Should().BeEmpty();
        _merged.Should().ContainSingle();
    }

    [Fact]
    public async Task OnCiGreen_RefusedApproval_LeavesItOpen_NamingTheKey()
    {
        _approval = PullRequestApproval.Refused("Can not approve your own pull request");

        var result = await RunAsync(OnCi(respect: true), passed: null, Green("a"));

        _merged.Should().BeEmpty();
        result.Message.Should().Contain("Can not approve your own pull request").And.Contain("merge.approval_auth");
        _outcomes.Should().AllSatisfy(o => o.Status.Should().Be(PullRequestStatuses.CompletionRefused));
    }

    private static Action<PipelineContext> Green(params string[] repos) => pipeline => pipeline.Set(ContextKeys.CiGreenMerge,
        new CiGreenMergeRequest("42", "attempt-1", "agent-smith/42", [.. repos.Select(r => new CiGreenPullRequest(r, $"https://x/{r}/pull/1"))]));

    private static MergePolicyConfig OnCi(bool respect = false) =>
        new() { When = MergeWhen.OnCiGreen, RespectBranchProtection = respect };

    private static MergePolicyConfig OnGreen(bool respect = false) =>
        new() { When = MergeWhen.OnGateGreen, RespectBranchProtection = respect };

    private static List<OpenedPullRequest> Passed(params string[] repos) =>
        [.. repos.Select(r => new OpenedPullRequest(r, $"https://x/{r}/pull/1", OpenStatus.Opened))];

    private async Task<CommandResult> RunAsync(
        MergePolicyConfig policy, List<OpenedPullRequest>? passed, Action<PipelineContext>? arrange = null)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-1");
        if (passed is not null) pipeline.Set<IReadOnlyList<OpenedPullRequest>>(ContextKeys.GatePassedPullRequests, passed);
        arrange?.Invoke(pipeline);
        var repos = new[] { "a", "b" }.Select(n => new RepoConnection { Name = n, Type = RepoType.GitHub }).ToList();
        var provider = new Mock<ISourceProvider>();
        provider.As<IPullRequestApprover>().Setup(a => a.ApproveIfRequiredAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((url, _) =>
            {
                _approved.Add(url);
                return Task.FromResult(_approval);
            });
        provider.Setup(p => p.MergePullRequestAsync(It.IsAny<string>(), It.IsAny<BranchName>(), It.IsAny<BranchProtection>(), It.IsAny<CancellationToken>()))
            .Returns<string, BranchName, BranchProtection, CancellationToken>((url, _, mode, _) =>
            {
                _merged.Add((url, mode));
                return Task.FromResult(_answers.GetValueOrDefault(url) ?? PullRequestCompletion.Merged());
            });
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(provider.Object);
        var events = new Mock<IEventPublisher>();
        events.Setup(e => e.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback<RunEvent, CancellationToken>((e, _) => { if (e is PullRequestOutcomeEvent o) _outcomes.Add(o); })
            .Returns(Task.CompletedTask);
        var completer = new PullRequestCompleter(factory.Object, events.Object, NullLogger<PullRequestCompleter>.Instance);
        var handler = new CompletePullRequestsHandler(new PullRequestMergeSequence(completer, new PullRequestApprovalStep(factory.Object),
            NullLogger<PullRequestMergeSequence>.Instance), completer);
        return await handler.ExecuteAsync(
            new CompletePullRequestsContext(policy, new BranchName("agent-smith/42"), repos, pipeline), CancellationToken.None);
    }
}
