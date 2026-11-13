using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>2026-11-13-3f79: a draft a pass of this turn passed is settled — stopped with its settled rounds.</summary>
public sealed class ProposalReviewCarryOverTests
{
    [Fact]
    public void SettledDraft_StoppedWithItsRounds()
    {
        var (pipeline, round) = Settled(SpecReviewVerdicts.BuildWithCuts);

        var review = ProposalReviewCarryOver.Seed(pipeline, [Draft("spec: a, cuts applied")]).Single();

        review.Stopped.Should().BeTrue();
        review.Settled.Should().BeTrue();
        review.Rounds.Should().ContainSingle().Which.Should().BeSameAs(round);
    }

    [Fact]
    public void SettledEdited_AttackedYamlUnset()
    {
        var (pipeline, _) = Settled(SpecReviewVerdicts.Build);

        var review = ProposalReviewCarryOver.Seed(pipeline, [Draft("spec: a, edited")]).Single();

        review.AttackedYaml.Should().BeNull();
        review.ReviewedClean.Should().BeFalse();
    }

    [Fact]
    public void RefusedEdited_IsOpen()
    {
        var (pipeline, _) = Settled(SpecReviewVerdicts.Refuse);

        var review = ProposalReviewCarryOver.Seed(pipeline, [Draft("spec: a, rewritten")]).Single();

        review.Stopped.Should().BeFalse("a refused draft is attacked again over its new text");
        review.Rounds.Should().BeEmpty();
    }

    private static (PipelineContext Pipeline, ProposalReviewRound Round) Settled(string verdict)
    {
        var pipeline = new PipelineContext();
        var round = new ProposalReviewRound("a", 1, verdict, [verdict], [], [], []);
        ProposalReviewLedger.Of(pipeline).Record(new SpecOutcome(Draft("spec: a")) with { ReviewRounds = [round] });
        return (pipeline, round);
    }

    private static SpecDraft Draft(string yaml) => new("a", "goal of a", yaml, []);
}
