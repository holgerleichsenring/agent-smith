using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.Rework;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static AgentSmith.Tests.Rework.Records.PrReviewRefreshHarness;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>2026-11-24-a4e7: an act's refresh at once — the worker's refresh under the worker's ticket lock, so a merge the
/// host just did is on the records before the act answers, then one Refresh nudge so the worker runs the CI loop; a busy
/// lock reads nothing and leaves it to that nudge; nothing it meets is thrown at the act.</summary>
public sealed class PrRecordRefreshNowTests : IDisposable
{
    private readonly PrReviewRefreshHarness _h = new();
    private readonly Mock<IRedisClaimLock> _locks = new();

    public void Dispose() => _h.Dispose();

    private PrRecordRefreshNow Now(IReworkNudges? nudges = null) => new(_h.Refresh(), _locks.Object,
        nudges ?? _h.Store.Services.GetRequiredService<IReworkNudges>(), NullLogger<PrRecordRefreshNow>.Instance);

    private static string Key => ReworkNudgeWorker.LockKey("p", "7");

    private List<ReworkNudge> Nudges() => [.. _h.Store.Context().Set<ReworkNudge>()];

    [Fact]
    public async Task RefreshAsync_AFreeLock_ReadsTheMergeFromTheHost_ReleasesTheLock_AndQueuesTheWorkersRefresh()
    {
        await _h.RunAsync("run-1", Pr1);
        _h.State(Pr1, PrReviewStates.Merged);
        _locks.Setup(l => l.TryAcquireAsync(Key, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ReturnsAsync("token");

        await Now().RefreshAsync(Project, "7");

        _h.Records().Single().State.Should().Be(PrReviewStates.Merged);
        _locks.Verify(l => l.ReleaseAsync(Key, "token", It.IsAny<CancellationToken>()), Times.Once);
        Nudges().Should().ContainSingle().Which.Origin.Should().Be((int)ReworkNudgeOrigin.Refresh);
    }

    [Fact]
    public async Task RefreshAsync_ABusyLock_ReadsNothing_AndQueuesOneRefreshNudge()
    {
        await _h.RunAsync("run-1", Pr1);
        _h.State(Pr1, PrReviewStates.Merged);
        _locks.Setup(l => l.TryAcquireAsync(Key, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        await Now().RefreshAsync(Project, "7");

        _h.Records().Should().BeEmpty();
        Nudges().Should().ContainSingle()
            .Which.Should().Match<ReworkNudge>(n => n.Project == "p" && n.TicketId == "7" && n.Origin == (int)ReworkNudgeOrigin.Refresh);
    }

    [Fact]
    public async Task RefreshAsync_ALockThatThrows_StillQueuesTheNudge_AndThrowsNothing()
    {
        _locks.Setup(l => l.TryAcquireAsync(Key, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis is gone"));

        await Now().Invoking(n => n.RefreshAsync(Project, "7")).Should().NotThrowAsync();

        Nudges().Should().ContainSingle();
    }

    [Fact]
    public async Task RefreshAsync_ANudgeQueueThatThrows_ThrowsNothing()
    {
        _locks.Setup(l => l.TryAcquireAsync(Key, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        var nudges = new Mock<IReworkNudges>();
        nudges.Setup(n => n.EnqueueAsync(It.IsAny<ReworkNudgeRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Now(nudges.Object).Invoking(n => n.RefreshAsync(Project, "7")).Should().NotThrowAsync();
    }
}
