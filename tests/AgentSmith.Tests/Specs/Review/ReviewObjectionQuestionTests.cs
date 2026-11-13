using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Events;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-11-13-b3a2: a standing objection's owner decision is asked on the ticket as the question it is.</summary>
public sealed class ReviewObjectionQuestionTests
{
    [Fact]
    public void Decision_RenderedAsQuestion()
    {
        var handback = ReviewObjectionQuestion.For([new StandingObjection("2026-11-13-0001a", [
            new SpecReviewFinding("the policy is only proposed", "ref/p.md:3 [A1]", "is the retention policy approved"),
            new SpecReviewFinding("the handler does not exist", "src/H.cs:1")])]);

        handback.Reason.Should().Contain($"{ReviewObjectionQuestion.OwnerQuestion} is the retention policy approved?");
        handback.Reason.Split(ReviewObjectionQuestion.OwnerQuestion).Should().HaveCount(2, "only one finding carries a decision");
    }
}
