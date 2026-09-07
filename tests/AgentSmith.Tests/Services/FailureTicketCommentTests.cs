using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-09-07-f420: the ticket's one comment on a failed run — the failure, and what
/// the finalizer tail kept and where.
/// </summary>
public sealed class FailureTicketCommentTests
{
    private readonly FailureTicketComment _sut = new();

    [Fact]
    public void ForStep_NamesTheStepAndTheEncodedError()
    {
        var failure = CommandResult.Fail("a <b>raw</b> error") with { FailedStep = 2, TotalSteps = 3, StepName = "Test" };

        var comment = _sut.ForStep(failure, new PipelineContext());

        comment.Should().StartWith("<b>Agent Smith — Failed</b>")
            .And.Contain("<b>Step:</b> Test (2/3)")
            .And.Contain("&lt;b&gt;raw&lt;/b&gt;")
            .And.NotContain("Partial work");
    }

    [Fact]
    public void ForStep_WorkPersistedByTheTail_NamesTheDraftPrAndBranch()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName("agent-smith/ticket-7"), "/work"));
        pipeline.Set<IReadOnlyList<OpenedPullRequest>>(ContextKeys.OpenedPullRequests,
            [new OpenedPullRequest("primary", "https://stub.test/pulls/7", OpenStatus.Opened)]);

        var comment = _sut.ForStep(CommandResult.Fail("boom"), pipeline);

        comment.Should().Contain("https://stub.test/pulls/7").And.Contain("agent-smith/ticket-7")
            .And.Contain("not completed").And.NotContain("Completed across");
    }

    [Fact]
    public void PersistedWork_NothingOpened_IsEmpty()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<OpenedPullRequest>>(ContextKeys.OpenedPullRequests,
            [new OpenedPullRequest("primary", null, OpenStatus.Failed, "push failed")]);

        _sut.PersistedWork(pipeline).Should().BeEmpty();
        _sut.PersistedWork(new PipelineContext()).Should().BeEmpty();
    }

    [Fact]
    public void ForFatal_EmptyMessage_FallsBackToTheExceptionType()
    {
        _sut.ForFatal(new InvalidOperationException(string.Empty))
            .Should().Contain("<b>Error:</b> InvalidOperationException");
    }

    [Fact]
    public void ForMessage_EncodesTheMessageUnderTheHeading()
    {
        _sut.ForMessage("the sandbox <vanished>")
            .Should().Be("<b>Agent Smith — Failed</b><br/>the sandbox &lt;vanished&gt;");
    }
}
