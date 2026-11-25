using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.Rework;
using FluentAssertions;

namespace AgentSmith.Tests.Rework.Ci;

/// <summary>
/// 2026-11-25-7a92a: a CI read vouches for a settled head only when the head it reads is the one stored and has stood there —
/// since it moved, else since the pull request opened — for merge.ci_settle_seconds.
/// </summary>
public sealed class PrRecordCiReadSettledTests
{
    private static readonly DateTimeOffset Opened = new(2026, 11, 25, 8, 0, 0, TimeSpan.Zero);
    private static readonly ResolvedProject Project = new() { Name = "p", Merge = new MergePolicyConfig { CiSettleSeconds = 180 } };

    [Theory]
    [InlineData(179, false)]
    [InlineData(180, true)]
    public void Settled_TheStoredHeadSinceItOpened_CountsTheSettleTime(int seconds, bool expected) =>
        PrRecordCiRead.Settled(Project, Read("abc", seconds), Stored("abc", movedAt: null)).Should().Be(expected);

    [Fact]
    public void Settled_AHeadThatMoved_CountsFromTheMove() =>
        PrRecordCiRead.Settled(Project, Read("abc", 600), Stored("abc", movedAt: Opened.AddSeconds(500))).Should().BeFalse();

    [Fact]
    public void Settled_AHeadTheRecordHasNotSeen_IsFresh() =>
        PrRecordCiRead.Settled(Project, Read("new", 3600), Stored("abc", movedAt: null)).Should().BeFalse();

    [Fact]
    public void Settled_NoStoredRecord_IsFresh() => PrRecordCiRead.Settled(Project, Read("abc", 3600), null).Should().BeFalse();

    private static PrReviewRecord Read(string head, int secondsAfterOpen) =>
        new() { HeadSha = head, CheckedAtTicks = Opened.AddSeconds(secondsAfterOpen).UtcTicks };

    private static PrReviewRecordView Stored(string head, DateTimeOffset? movedAt) =>
        new("k", "https://github.com/example/sample-server/pull/7", "api", "7", "p", "7", "r", PrReviewStates.Open, Opened, Opened, null, [])
        { HeadSha = head, HeadMovedAt = movedAt };
}
