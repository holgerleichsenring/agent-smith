using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// 2026-11-17-023fa: CommitAndPR's success tail publishes the pull requests a merge may take — only for a mergeable
/// (code-changing, non-draft) run where no repository failed to open.
/// </summary>
public sealed class CodeRunResultGateTests
{
    [Fact]
    public void MergeableRun_PublishesItsOpenedPullRequests()
    {
        var pipeline = new PipelineContext();

        CodeRunResult.PublishGatePassed(pipeline, [Opened("a"), new OpenedPullRequest("b", null, OpenStatus.SkippedNoChanges)], mergeable: true);

        pipeline.Get<IReadOnlyList<OpenedPullRequest>>(ContextKeys.GatePassedPullRequests)
            .Select(o => o.RepoName).Should().Equal("a");
    }

    [Fact]
    public void NotMergeable_PublishesNothing()
    {
        var pipeline = new PipelineContext();

        CodeRunResult.PublishGatePassed(pipeline, [Opened("a")], mergeable: false);

        pipeline.Has(ContextKeys.GatePassedPullRequests).Should().BeFalse();
    }

    [Fact]
    public void AFailedRepository_PublishesNothing()
    {
        var pipeline = new PipelineContext();

        CodeRunResult.PublishGatePassed(pipeline, [Opened("a"), new OpenedPullRequest("b", null, OpenStatus.Failed, "push refused")], mergeable: true);

        pipeline.Has(ContextKeys.GatePassedPullRequests).Should().BeFalse("half a cross-repository change must not land");
    }

    private static OpenedPullRequest Opened(string repo) => new(repo, $"https://x/{repo}/pull/1", OpenStatus.Opened);
}
