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
/// host just did is on the records before the act answers; a busy lock queues a Refresh nudge instead.</summary>
public sealed class PrRecordRefreshNowTests : IDisposable
{
    private readonly PrReviewRefreshHarness _h = new();
    private readonly Mock<IRedisClaimLock> _locks = new();

    public void Dispose() => _h.Dispose();

    private PrRecordRefreshNow Now() => new(_h.Refresh(), _locks.Object, _h.Store.Services.GetRequiredService<IReworkNudges>(),
        NullLogger<PrRecordRefreshNow>.Instance);

    private static string Key => ReworkNudgeWorker.LockKey("p", "7");

    [Fact]
    public async Task RefreshAsync_AFreeLock_ReadsTheMergeFromTheHost_AndReleasesTheLock()
    {
        await _h.RunAsync("run-1", Pr1);
        _h.State(Pr1, PrReviewStates.Merged);
        _locks.Setup(l => l.TryAcquireAsync(Key, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ReturnsAsync("token");

        await Now().RefreshAsync(Project, "7", CancellationToken.None);

        _h.Records().Single().State.Should().Be(PrReviewStates.Merged);
        _locks.Verify(l => l.ReleaseAsync(Key, "token", It.IsAny<CancellationToken>()), Times.Once);
        _h.Store.Context().Set<ReworkNudge>().Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshAsync_ABusyLock_QueuesOneRefreshNudge_AndReadsNothing()
    {
        await _h.RunAsync("run-1", Pr1);
        _h.State(Pr1, PrReviewStates.Merged);
        _locks.Setup(l => l.TryAcquireAsync(Key, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        await Now().RefreshAsync(Project, "7", CancellationToken.None);

        _h.Records().Should().BeEmpty();
        _h.Store.Context().Set<ReworkNudge>().Should().ContainSingle()
            .Which.Should().Match<ReworkNudge>(n => n.Project == "p" && n.TicketId == "7" && n.Origin == (int)ReworkNudgeOrigin.Refresh);
    }
}
