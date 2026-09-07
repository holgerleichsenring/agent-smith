using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// 2026-09-07-f420: the wording of a failed run's persisted work — nothing it says may
/// read as a completed run.
/// </summary>
public sealed class FailedRunPersistenceTests
{
    private readonly FailedRunPersistence _sut = new();

    [Fact]
    public void Reason_NoFailureOnTheContext_IsNull()
    {
        var pipeline = new PipelineContext();

        _sut.Reason(pipeline).Should().BeNull();
    }

    [Fact]
    public void Reason_AFailedStepLeftOne_IsTheReason()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.FailureReason, "per-pipeline cost budget exhausted");

        _sut.Reason(pipeline).Should().Be("per-pipeline cost budget exhausted");
    }

    [Fact]
    public void PullRequestBanner_NamesTheFailure()
    {
        var banner = _sut.PullRequestBanner("per-pipeline cost budget exhausted");

        banner.Should().StartWith("> ⚠️ **Run failed** — per-pipeline cost budget exhausted")
            .And.Contain("do not merge").And.NotContain("Completed");
    }

    [Fact]
    public void StepResult_WithADraftPr_NamesItAndNeverSaysCompleted()
    {
        var result = _sut.StepResult("budget exhausted",
            [new OpenedPullRequest("primary", "https://stub.test/pulls/7", OpenStatus.Opened)]);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Run failed").And.Contain("https://stub.test/pulls/7")
            .And.NotContain("Completed");
    }

    [Fact]
    public void StepResult_NothingOpened_SaysNothingWasPersisted()
    {
        var result = _sut.StepResult("budget exhausted",
            [new OpenedPullRequest("primary", null, OpenStatus.SkippedNoChanges)]);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("nothing to persist").And.NotContain("Completed");
    }
}
