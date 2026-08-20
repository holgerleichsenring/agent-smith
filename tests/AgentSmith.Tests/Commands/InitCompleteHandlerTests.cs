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
/// p0490: the init pipeline's last step finishes the pull requests InitCommit opened —
/// but only when the LAUNCH carried the operator's auto-accept, only for repos that
/// actually opened one, and never at the cost of the run's result: a completion the
/// platform refuses leaves the pull request open and records the reason for that repo.
/// </summary>
public sealed class InitCompleteHandlerTests
{
    private const string Branch = "agentsmith/init";

    [Fact]
    public async Task InitComplete_FlagOff_LeavesEveryPullRequestOpen()
    {
        var harness = new Harness()
            .WithOpened("a", "https://x/a/pull/1")
            .WithOpened("b", "https://x/b/pull/2");

        var result = await harness.RunAsync(autoComplete: false);

        result.IsSuccess.Should().BeTrue();
        harness.Completed.Should().BeEmpty("no consent was given on this launch");
        harness.Outcomes.Should().BeEmpty("nothing happened, so nothing is recorded");
        result.Message.Should().Contain("stay open");
    }

    [Fact]
    public async Task InitComplete_FlagOn_CompletesEachOpenedPullRequest()
    {
        var harness = new Harness()
            .WithOpened("a", "https://x/a/pull/1")
            .WithOpened("b", "https://x/b/pull/2");

        var result = await harness.RunAsync(autoComplete: true);

        result.IsSuccess.Should().BeTrue();
        harness.Completed.Should().BeEquivalentTo(["https://x/a/pull/1", "https://x/b/pull/2"]);
        harness.Outcomes.Should().AllSatisfy(o => o.Status.Should().Be(PullRequestStatuses.Completed));
        harness.Outcomes.Select(o => o.Url).Should().BeEquivalentTo(
            ["https://x/a/pull/1", "https://x/b/pull/2"],
            "a completed row still links to what was merged");
    }

    [Fact]
    public async Task InitComplete_PolicyRefusesTheMerge_PullRequestStaysOpen_RunKeepsItsResult()
    {
        var harness = new Harness()
            .WithOpened("a", "https://x/a/pull/1")
            .WithRefusal("b", "https://x/b/pull/2", "At least 1 approving review is required.");

        var result = await harness.RunAsync(autoComplete: true);

        result.IsSuccess.Should().BeTrue("a refused completion is not a failed run");
        harness.Completed.Should().ContainSingle().Which.Should().Be("https://x/a/pull/1");
        result.Message.Should().Contain("1/2");
    }

    [Fact]
    public async Task InitComplete_RefusalReason_IsRecordedPerRepo()
    {
        var harness = new Harness()
            .WithOpened("a", "https://x/a/pull/1")
            .WithRefusal("b", "https://x/b/pull/2", "At least 1 approving review is required.");

        await harness.RunAsync(autoComplete: true);

        var refused = harness.Outcomes.Single(o => o.Repo == "b");
        refused.Status.Should().Be(PullRequestStatuses.CompletionRefused);
        refused.Reason.Should().Contain("approving review is required");
        refused.Url.Should().Be("https://x/b/pull/2", "the pull request is still there to look at");
        harness.Outcomes.Single(o => o.Repo == "a").Status.Should().Be(PullRequestStatuses.Completed);
    }

    [Fact]
    public async Task InitComplete_SkippedNoChangesRepo_IsNotCompleted()
    {
        var harness = new Harness()
            .WithOpened("a", "https://x/a/pull/1")
            .WithSkipped("b")
            .WithFailedOpen("c");

        await harness.RunAsync(autoComplete: true);

        harness.Completed.Should().ContainSingle().Which.Should().Be("https://x/a/pull/1");
        harness.Outcomes.Select(o => o.Repo).Should().BeEquivalentTo(["a"]);
    }

    [Fact]
    public async Task InitComplete_ProviderThrows_IsRecordedAsRefused_AndTheRunSurvives()
    {
        var harness = new Harness()
            .WithThrowingProvider("a", "https://x/a/pull/1", "the remote hung up");

        var result = await harness.RunAsync(autoComplete: true);

        result.IsSuccess.Should().BeTrue();
        harness.Outcomes.Single().Status.Should().Be(PullRequestStatuses.CompletionRefused);
        harness.Outcomes.Single().Reason.Should().Contain("the remote hung up");
    }

    [Fact]
    public async Task InitComplete_NoOpenedPullRequests_IsOkAndCompletesNothing()
    {
        var harness = new Harness();

        var result = await harness.RunAsync(autoComplete: true);

        result.IsSuccess.Should().BeTrue();
        harness.Completed.Should().BeEmpty();
    }

    private sealed class Harness
    {
        public List<string> Completed { get; } = [];
        public List<PullRequestOutcomeEvent> Outcomes { get; } = [];

        private readonly List<RepoConnection> _repos = [];
        private readonly List<OpenedPullRequest> _opened = [];
        private readonly Mock<ISourceProviderFactory> _sources = new();

        public Harness WithOpened(string name, string prUrl) =>
            WithProvider(name, prUrl, _ => PullRequestCompletion.Merged());

        public Harness WithRefusal(string name, string prUrl, string reason) =>
            WithProvider(name, prUrl, _ => PullRequestCompletion.Refused(reason));

        public Harness WithThrowingProvider(string name, string prUrl, string message) =>
            WithProvider(name, prUrl, _ => throw new InvalidOperationException(message));

        public Harness WithSkipped(string name)
        {
            AddRepo(name);
            _opened.Add(new OpenedPullRequest(name, Url: null, OpenStatus.SkippedNoChanges));
            return this;
        }

        public Harness WithFailedOpen(string name)
        {
            AddRepo(name);
            _opened.Add(new OpenedPullRequest(name, Url: null, OpenStatus.Failed, "boom"));
            return this;
        }

        public async Task<CommandResult> RunAsync(bool autoComplete)
        {
            var pipeline = new PipelineContext();
            pipeline.Set(ContextKeys.RunId, "2026-08-20T09-00-00-abcd");
            pipeline.Set<IReadOnlyList<OpenedPullRequest>>(ContextKeys.OpenedPullRequests, _opened);
            var handler = new InitCompleteHandler(
                _sources.Object, NewPublisher(), NullLogger<InitCompleteHandler>.Instance);
            return await handler.ExecuteAsync(
                new InitCompleteContext(autoComplete, new BranchName(Branch), _repos, pipeline),
                CancellationToken.None);
        }

        private Harness WithProvider(
            string name, string prUrl, Func<string, PullRequestCompletion> answer)
        {
            AddRepo(name);
            _opened.Add(new OpenedPullRequest(name, prUrl, OpenStatus.Opened));
            var provider = new Mock<ISourceProvider>();
            provider.Setup(p => p.CompletePullRequestAsync(
                    It.IsAny<string>(), It.IsAny<BranchName>(), It.IsAny<CancellationToken>()))
                .Returns<string, BranchName, CancellationToken>((url, branch, _) =>
                {
                    branch.Value.Should().Be(Branch, "a local repo needs the work branch to fast-forward");
                    var completion = answer(url);
                    if (completion.Completed) Completed.Add(url);
                    return Task.FromResult(completion);
                });
            _sources.Setup(f => f.Create(It.Is<RepoConnection>(r => r.Name == name)))
                .Returns(provider.Object);
            return this;
        }

        private void AddRepo(string name) =>
            _repos.Add(new RepoConnection { Name = name, Type = RepoType.GitHub, Url = $"https://x/{name}.git" });

        private IEventPublisher NewPublisher()
        {
            var publisher = new Mock<IEventPublisher>();
            publisher.Setup(p => p.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
                .Callback<RunEvent, CancellationToken>((e, _) =>
                {
                    if (e is PullRequestOutcomeEvent outcome) Outcomes.Add(outcome);
                })
                .Returns(Task.CompletedTask);
            return publisher.Object;
        }
    }
}
