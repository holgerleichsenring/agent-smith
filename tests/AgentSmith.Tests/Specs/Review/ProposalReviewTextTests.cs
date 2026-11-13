using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-11-13-b3a2: a draft refused on owner decisions only waits on the owner and is passed for the turn;
/// a refusal that also names a defect is not.</summary>
public sealed class ProposalReviewTextTests
{
    private static readonly SpecReviewFinding Decision = new("the policy is only proposed", "ref/p.md:3 [A1]", "Is the policy approved?");
    private static readonly SpecReviewFinding Defect = new("the handler does not exist", "shop/src/H.cs [A2]");

    [Fact]
    public void RefuseAllFindingsDecisions_Passed()
    {
        Text().PassedIn(Refused(Decision)).Should().BeTrue();
        ReviewOwnerDecision.Waits(Refused(Decision).ReviewRounds[0]).Should().BeTrue();
    }

    [Fact]
    public void RefuseMixed_NotPassed()
    {
        Text().PassedIn(Refused(Decision, Defect)).Should().BeFalse();
        Text().PassedIn(Refused()).Should().BeFalse("a refusal with no finding waits on nothing");
    }

    [Fact]
    public void Carries_BlankDecision_False() =>
        ReviewOwnerDecision.Carries(Defect with { Decision = " " }).Should().BeFalse();

    [Fact]
    public void Question_EndsInAQuestionMark()
    {
        ReviewOwnerDecision.Question(" is it approved. ").Should().Be("is it approved?");
        ReviewOwnerDecision.Question("Is it approved?").Should().Be("Is it approved?");
    }

    private static ProposalReviewText Text() => new("a:hash", "a", "a");

    private static OutcomeProposal Refused(params SpecReviewFinding[] findings) =>
        new SpecOutcome(new SpecDraft("a", "goal", "spec: a", [])) with
        {
            ReviewRounds = [new ProposalReviewRound("a", 1, SpecReviewVerdicts.Refuse, [SpecReviewVerdicts.Refuse], [], findings, [])],
        };
}
