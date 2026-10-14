using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Commands;

/// <summary>2026-10-13-8762u: an attempt that stopped at a series boundary — the pull request stays a draft with
/// the series banner, the ticket gets ONE comment naming where the next attempt continues, and nothing finalizes.</summary>
public partial class CommitAndPRHandlerTests
{
    private const string BoundaryReason = "beyond this attempt's chunk of 2 spec(s) (series.specs_per_attempt and the wall clock left)";

    [Fact]
    public async Task CommitAndPR_ChunkBoundary_DraftPrWithSeriesBanner_TicketNotFinalized()
    {
        var (body, draft) = CapturePullRequest();
        var pipeline = ChunkPipeline(SpecRunState.Done);

        var result = await _sut.ExecuteAsync(CreateContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("Series in progress: 2 of 3 specs built; the next attempt continues with p0001c.");
        draft().Should().BeTrue("the repository holds part of a series");
        body().Should().StartWith("> 🧩 **Series in progress: 2 of 3 specs built; the next attempt continues with p0001c.**")
            .And.NotContain("Verification red")
            .And.Contain("## 🧩 Series in progress — do not merge until it is through")
            .And.Contain("| p0001c | Goal c | ⏭️ next attempt |");
        _ticketProviderMock.Verify(t => t.FinalizeAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        RunShortfall.DeliveredOn(pipeline).Should().BeNull();
    }

    [Fact]
    public async Task CommitAndPR_ChunkBoundary_PostsOneCommentNamingTheNextSpec()
    {
        CapturePullRequest();

        await _sut.ExecuteAsync(CreateContext(ChunkPipeline(SpecRunState.Done)), CancellationToken.None);

        _ticketProviderMock.Verify(t => t.UpdateStatusAsync(It.Is<TicketId>(id => id.Value == "123"),
            It.Is<string>(c => c.Contains("the next attempt continues with p0001c") && c.Contains("Built: p0001a, p0001b")
                && c.Contains("Left: p0001c") && c.Contains("comment on this ticket") && c.Contains("Request changes")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CommitAndPR_ChunkBoundary_InlineTicket_PostsNothing()
    {
        CapturePullRequest();
        var pipeline = ChunkPipeline(SpecRunState.Done);
        pipeline.Set(ContextKeys.InlineTicket, true);

        var result = await _sut.ExecuteAsync(CreateContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _ticketProviderMock.Verify(t => t.UpdateStatusAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CommitAndPR_LastChunk_FinalizesTheTicketAsToday()
    {
        var (_, draft) = CapturePullRequest();
        var pipeline = NewPipelineWithSandbox();
        pipeline.Set(ContextKeys.SpecSequenceProgress, new SpecSequenceProgress([
            new SpecProgress("p0001a", "Goal a", SpecRunState.Done), new SpecProgress("p0001b", "Goal b", SpecRunState.Done)]));

        var result = await _sut.ExecuteAsync(CreateContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        draft().Should().BeFalse();
        _ticketProviderMock.Verify(t => t.FinalizeAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _ticketProviderMock.Verify(t => t.UpdateStatusAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CommitAndPR_FailedChunk_TakesTheFailedRunPathNotTheChunkArm()
    {
        var (body, _) = CapturePullRequest();
        var pipeline = ChunkPipeline(SpecRunState.Failed);
        pipeline.Set(ContextKeys.FailureReason, "verification red on p0001b");

        var result = await _sut.ExecuteAsync(CreateContext(pipeline), CancellationToken.None);

        result.Message.Should().Contain("Run failed").And.NotContain("Series in progress");
        body().Should().StartWith("> ⚠️ **Run failed**").And.Contain("## ⛔ Half-migrated — DO NOT MERGE")
            .And.Contain($"⬜ not started — {BoundaryReason}");
        _ticketProviderMock.Verify(t => t.UpdateStatusAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private PipelineContext ChunkPipeline(SpecRunState second)
    {
        var pipeline = NewPipelineWithSandbox();
        pipeline.Set(ContextKeys.PipelineName, "code");
        pipeline.Set(ContextKeys.DoneStatus, "done");
        pipeline.Set(ContextKeys.SpecSet, new SpecSet("github-123",
            [.. new[] { "p0001a", "p0001b", "p0001c" }.Select(id => new DerivedSpec(new SpecDraft(id, $"Goal {id[^1]}", $"spec: {id}", []), id, string.Empty, []))],
            SpecAccounting.Empty, [new SpecRevision(1, "initial derivation", DateTimeOffset.UnixEpoch)], SpecSource.Derived));
        pipeline.Set(ContextKeys.SpecSequenceProgress, new SpecSequenceProgress([
            new SpecProgress("p0001a", "Goal a", SpecRunState.Done),
            new SpecProgress("p0001b", "Goal b", second, second == SpecRunState.Failed ? "VerifySpec" : null),
            new SpecProgress("p0001c", "Goal c", SpecRunState.NotStarted)]).Deferring(["p0001c"], BoundaryReason));
        return pipeline;
    }

    private (Func<string?> Body, Func<bool> Draft) CapturePullRequest()
    {
        string? body = null;
        var draft = false;
        _sourceProviderMock.Setup(s => s.CreatePullRequestAsync(
                It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>(), It.IsAny<TicketId?>(), It.IsAny<bool>(), It.IsAny<BranchName?>()))
            .Callback<Repository, string, string, CancellationToken, TicketId?, bool, BranchName?>(
                (_, _, b, _, _, d, _) => { body = b; draft = d; })
            .ReturnsAsync("https://github.com/test/repo/pull/42");
        return (() => body, () => draft);
    }
}
