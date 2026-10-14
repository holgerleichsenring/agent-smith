using AgentSmith.Contracts.Runs;
using AgentSmith.Server.Services.Rework;
using FluentAssertions;

namespace AgentSmith.Tests.Rework;

/// <summary>2026-10-14-dbcei: a refused rework names the act by the channel it came from.</summary>
public sealed class ReworkTextsTests
{
    [Theory]
    [InlineData(ReworkChannel.Ticket, "this comment")]
    [InlineData(ReworkChannel.Status, "this status change")]
    [InlineData(ReworkChannel.Operator, "the request in this ticket's conversation")]
    [InlineData(ReworkChannel.Retry, "this retry")]
    [InlineData(ReworkChannel.Overrule, "this overrule")]
    [InlineData(ReworkChannel.PullRequest, "this review")]
    public void ReworkTexts_TicketRefusal_NamesTheActsSourcePerChannel(ReworkChannel channel, string source) =>
        ReworkTexts.TicketRefusal("the queue is full", channel).Should().Be($"Agent Smith — {source} did not start a rework: the queue is full.");

    [Fact]
    public void ReworkTexts_TicketRefusal_WithoutAChannel_IsTheCommentSentence() =>
        ReworkTexts.TicketRefusal("x").Should().Be("Agent Smith — this comment did not start a rework: x.");
}
