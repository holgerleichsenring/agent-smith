using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Reviews;
using FluentAssertions;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-25-9992a: the one rule a pull request's CI is read by — the page's rows, the hold and the master.</summary>
public sealed class PullRequestHeadCiTests
{
    private static readonly DateTimeOffset T = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);

    private static PrReviewRecordView Record(PrRecordCiView? ci, string head = "abc") =>
        new("k", "https://host/pr/1", "api", "1", "alpha", "1", "run-1", PrReviewStates.Open, null, T, null, [])
        { HeadSha = head, HeadMovedAt = T.AddHours(1), Ci = ci };

    [Fact]
    public void OnHead_AReadOnTheHead_IsItself_AnOlderHeadsIsPendingSinceTheHeadMoved_NoneIsNull()
    {
        var read = new PrRecordCiView(CiStates.Green, "abc", T, []);

        PullRequestHeadCi.OnHead(Record(read)).Should().Be(read);
        PullRequestHeadCi.OnHead(Record(read, head: "def")).Should().BeEquivalentTo(new PrRecordCiView(CiStates.Pending, "def", T.AddHours(1), []));
        PullRequestHeadCi.OnHead(Record(null)).Should().BeNull();
    }

    [Theory]
    [InlineData(CiStates.Red, CiStates.Green, CiStates.Pending, CiStates.Red)]
    [InlineData(CiStates.Pending, CiStates.NotStarted, CiStates.Green, CiStates.NotStarted)]
    [InlineData(CiStates.Green, CiStates.None, null, CiStates.Green)]
    public void Of_ReadsSeveralHeadsByPrecedence(string? a, string? b, string? c, string expected) =>
        PullRequestHeadCi.Of([a, b, c]).Should().Be(expected);

    [Fact]
    public void Of_NothingRead_IsNull_AndFirstFailure_NamesARedHeadsFirstCheckAndLine()
    {
        PullRequestHeadCi.Of([null, null]).Should().BeNull();
        PullRequestHeadCi.FirstFailure(new PrRecordCiView(CiStates.Red, "abc", T, [new CiFailure("build", null, "CS0246\nmore")]))
            .Should().Be("build: CS0246");
        PullRequestHeadCi.FirstFailure(new PrRecordCiView(CiStates.Green, "abc", T, [])).Should().BeNull();
    }
}
