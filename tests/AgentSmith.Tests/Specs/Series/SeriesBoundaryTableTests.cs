using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Application.Services.Rework;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs.Series;

/// <summary>2026-10-13-8762u: what a boundary looks like — the progress says it stopped at a chunk, the table reads
/// "next attempt", the ticket waits, and the full-series notice states the series cap.</summary>
public sealed class SeriesBoundaryTableTests
{
    private const string Reason = "beyond this attempt's chunk of 1 spec(s) (series.specs_per_attempt and the wall clock left)";

    [Fact]
    public void SpecSequenceProgress_StoppedAtChunk_FalseWhenAChunkSpecFailed()
    {
        Progress(SpecRunState.Done).StoppedAtChunk.Should().BeTrue();
        Progress(SpecRunState.Failed).StoppedAtChunk.Should().BeFalse();
        Progress(SpecRunState.Done).With("a", SpecRunState.InProgress).StoppedAtChunk.Should().BeFalse();
        new SpecSequenceProgress([new SpecProgress("a", "A", SpecRunState.Done)]).StoppedAtChunk.Should().BeFalse();
    }

    [Fact]
    public void SpecStatusTable_NextAttemptSpecs_ReadNextAttemptNotNotStarted()
    {
        var table = SpecStatusTable.Render(Progress(SpecRunState.Done));

        table.Should().Contain("## 🧩 Series in progress — do not merge until it is through")
            .And.Contain("| b | B | ⏭️ next attempt |").And.NotContain("Half-migrated");
    }

    [Fact]
    public void SpecStatusTable_FailedRunWithNextAttemptSpecs_ShowsTheNoteNotNextAttempt()
    {
        var table = SpecStatusTable.Render(Progress(SpecRunState.Failed));

        table.Should().Contain("## ⛔ Half-migrated — DO NOT MERGE")
            .And.Contain($"| b | B | ⬜ not started — {Reason} |").And.NotContain("next attempt |");
    }

    [Fact]
    public async Task Lifecycle_ChunkBoundary_IsWaitingNotDone_RunNotParked()
    {
        var transitioner = new Mock<ITicketStatusTransitioner>();
        transitioner.Setup(t => t.TransitionAsync(It.IsAny<TicketId>(), It.IsAny<TicketLifecycleStatus>(),
            It.IsAny<TicketLifecycleStatus>(), It.IsAny<CancellationToken>())).ReturnsAsync(TransitionResult.Succeeded());
        var factory = new Mock<ITicketStatusTransitionerFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(transitioner.Object);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, new TicketId("PROJ-1"));
        pipeline.Set(ContextKeys.SpecSequenceProgress, Progress(SpecRunState.Done));
        var sut = new TicketAwarePipelineLifecycleCoordinator(factory.Object, NullLogger<TicketAwarePipelineLifecycleCoordinator>.Instance);

        await (await sut.BeginAsync(new ResolvedProject(), pipeline, CancellationToken.None)).DisposeAsync();

        transitioner.Verify(t => t.TransitionAsync(It.IsAny<TicketId>(), TicketLifecycleStatus.InProgress,
            TicketLifecycleStatus.Waiting, It.IsAny<CancellationToken>()), Times.Once);
        RunPark.IsWaitingForOperator(pipeline).Should().BeFalse("the run ends success, never waiting_for_input");
    }

    [Fact]
    public async Task FullSetPrNotice_SaysTheSeriesCap()
    {
        var comments = new Mock<IPrCommentProvider>();
        var provider = comments.As<ISourceProvider>();
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(s => s.Create(It.IsAny<RepoConnection>())).Returns(provider.Object);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ReworkAct, new ReworkAct("alice", DateTimeOffset.UnixEpoch, ReworkChannel.PullRequest));
        pipeline.Set(ContextKeys.PreviousAttempt, new PreviousAttempt("r-1", "completed", DateTimeOffset.UnixEpoch, true)
            { PullRequestUrls = new Dictionary<string, string> { ["api"] = "https://example.test/pr/7" } });
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, [new RepoConnection { Name = "api" }]);
        var notice = new FullSetPrNotice(sources.Object, SeriesSettingsDoubles.Reader(new() { MaxSpecs = 3, SpecsPerAttempt = 3 }),
            NullLogger<FullSetPrNotice>.Instance);

        await notice.PostAsync(pipeline, Executed(2), CancellationToken.None);
        await notice.PostAsync(pipeline, Executed(3), CancellationToken.None);

        comments.Verify(c => c.PostCommentAsync("7", It.Is<string>(m => m.Contains("all 3 specs of this ticket's series have run")
            && m.Contains("the most one series can hold")), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static SpecSequenceProgress Progress(SpecRunState first) =>
        new SpecSequenceProgress([new SpecProgress("a", "A", first), new SpecProgress("b", "B", SpecRunState.NotStarted)])
            .Deferring(["b"], Reason);

    private static SpecSet Executed(int count)
    {
        var ids = Enumerable.Range(0, count).Select(i => $"p{i}").ToList();
        return new SpecSet("k", [.. ids.Select(id => new DerivedSpec(new SpecDraft(id, id, $"spec: {id}", []), id, string.Empty, []))],
            SpecAccounting.Empty, [new SpecRevision(1, SpecRevisionCause.Rework, DateTimeOffset.UnixEpoch)],
            SpecSource.BranchArtifact, ExecutedSpecIds: ids);
    }
}
