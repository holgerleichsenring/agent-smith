using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

/// <summary>2026-11-24-f9c5d: a pull request with no CI has its CI started; started and impossible are said with the sentence
/// on the attempt and on the run. 2026-11-25-9992e: the series master asks for it and nothing merges with it — a repository
/// with no CI at all is said as such.</summary>
public sealed class PullRequestCiStartTests
{
    private readonly List<PullRequestOutcomeEvent> _said = [];
    private readonly Dictionary<string, CiTrigger> _answers = new(StringComparer.Ordinal);

    [Fact]
    public async Task Started_KeepsItAwaiting_WithTheSentence()
    {
        _answers["a"] = CiTrigger.Started("dispatched ci.yml on agent-smith/42", "https://x/actions");

        var said = await StartAsync(new CiGreenPullRequest("a", "https://x/a/pull/1") { NoCi = true });

        said.Should().Contain("dispatched ci.yml");
        _said.Should().ContainSingle(e => e.RunId == "attempt-1").Which.Should().Match<PullRequestOutcomeEvent>(e =>
            e.Status == PullRequestStatuses.AwaitingCi && e.Reason!.StartsWith("CI started: dispatched ci.yml"));
        _said.Should().ContainSingle(e => e.RunId == "run-ci").Which.Status.Should().Be(PullRequestStatuses.CompletionRefused,
            "the series page reads the merge run's own row");
    }

    [Fact]
    public async Task Impossible_SaysWhy()
    {
        _answers["a"] = CiTrigger.Impossible("ci.yml runs for pull requests into main only");

        await StartAsync(new CiGreenPullRequest("a", "https://x/a/pull/1") { NoCi = true });

        _said.Should().OnlyContain(e => e.Reason!.StartsWith("CI not started: ci.yml runs"));
    }

    [Fact]
    public async Task NoCiAnywhere_IsSaid()
    {
        _answers["a"] = CiTrigger.None("no workflow runs for pull requests into main");

        (await StartAsync(new CiGreenPullRequest("a", "https://x/a/pull/1") { NoCi = true })).Should().Contain("no workflow runs");
        _said.Should().NotBeEmpty().And.OnlyContain(e => e.Reason!.StartsWith("no CI: no workflow runs"));
    }

    [Fact]
    public async Task NothingWithoutCi_StartsNothing() =>
        (await StartAsync(new CiGreenPullRequest("a", "https://x/a/pull/1"))).Should().BeEmpty();

    private async Task<string> StartAsync(params CiGreenPullRequest[] prs)
    {
        var triggers = new Mock<IPullRequestCiTriggerFactory>();
        triggers.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns((RepoConnection r) =>
            Mock.Of<IPullRequestCiTrigger>(t => t.StartAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())
                == Task.FromResult(_answers.GetValueOrDefault(r.Name) ?? CiTrigger.None("none"))));
        var events = new Mock<IEventPublisher>();
        events.Setup(e => e.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback<RunEvent, CancellationToken>((e, _) => { if (e is PullRequestOutcomeEvent o) _said.Add(o); })
            .Returns(Task.CompletedTask);
        var completer = new PullRequestCompleter(Mock.Of<ISourceProviderFactory>(), events.Object, NullLogger<PullRequestCompleter>.Instance);
        var repos = new[] { "a", "b" }.Select(n => new RepoConnection { Name = n, Type = RepoType.GitHub, Url = $"https://x/{n}" }).ToList();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-ci");
        var context = new CompletePullRequestsContext(ReviewMode.End, BranchProtection.Respect, new BranchName("agent-smith/42"), repos, pipeline);
        return await new PullRequestCiStart(completer, NullLogger<PullRequestCiStart>.Instance, triggers.Object)
            .StartAsync(context, new CiGreenMergeRequest("42", "attempt-1", "agent-smith/42", prs), default);
    }
}
