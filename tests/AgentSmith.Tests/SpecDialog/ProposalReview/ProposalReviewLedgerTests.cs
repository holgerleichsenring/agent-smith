using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>2026-11-12-92e9b: the turn's review results, keyed by the exact text each pass reviewed.</summary>
public sealed class ProposalReviewLedgerTests
{
    [Fact]
    public void Of_SamePipeline_IsOneLedger()
    {
        var pipeline = new PipelineContext();

        ProposalReviewLedger.Of(pipeline).Should().BeSameAs(ProposalReviewLedger.Of(pipeline));
    }

    [Fact]
    public void Covers_ChangedByte_IsNotCovered()
    {
        var ledger = ProposalReviewLedger.Of(new PipelineContext());
        var reviewed = Reviewed(Draft("a", "spec: a"), "objection");

        ledger.Record(reviewed);

        ledger.Covers(new SpecOutcome(Draft("a", "spec: a"))).Should().BeTrue();
        ledger.Covers(new SpecOutcome(Draft("a", "spec: a "))).Should().BeFalse();
        ledger.Covers(new AnswerOutcome()).Should().BeTrue("an answer holds no text a pass reads");
        ledger.For(Draft("a", "spec: a")).Should().BeSameAs(reviewed);
    }

    [Fact]
    public void Compose_TextsFromTwoPasses_TakesEachTextsOwnResult()
    {
        var ledger = ProposalReviewLedger.Of(new PipelineContext());
        var (a, b) = (Draft("a", "spec: a"), Draft("b", "spec: b"));
        var parent = Draft("p", "spec: p");
        ledger.Record(new EpicOutcome(parent, [a, Draft("b", "spec: b old")]) with
        {
            Findings = [new ProposalFinding("a", SpecReviewVerdicts.Refuse, "first pass on a"), new ProposalFinding("p", "evidence", "parent")],
            ReviewRounds = [Round("a")],
        });
        ledger.Record(new EpicOutcome(parent, [Draft("a", "spec: a new"), b]) with
        {
            Findings = [new ProposalFinding("b", SpecReviewVerdicts.Refuse, "second pass on b")], ReviewRounds = [Round("b")],
        });

        var composed = ledger.Compose(new EpicOutcome(parent, [a, b]));

        composed.Findings.Select(f => f.Why).Should().Equal("first pass on a", "second pass on b");
        composed.ReviewRounds.Select(r => r.SpecId).Should().Equal("a", "b");
    }

    [Fact]
    public void Report_NothingReviewed_IsNull_ReviewedFindings_AreReported()
    {
        var capped = new SpecOutcome(Draft("a", "spec: a")) with
        {
            Findings = [new ProposalFinding("a", ProposalReviewSummary.NotReviewedYet, "cost cap")], NotReviewed = ["a"],
        };

        DesignTurnReviewReport.For(capped).Should().BeNull();
        DesignTurnReviewReport.For(Reviewed(Draft("a", "spec: a"), "objection")).Should()
            .StartWith(DesignTurnReviewReport.Heading).And.Contain($"a: {SpecReviewVerdicts.Refuse}").And.Contain("objection");
    }

    private static OutcomeProposal Reviewed(SpecDraft draft, string why) => new SpecOutcome(draft) with
    {
        Findings = [new ProposalFinding(draft.SpecId, SpecReviewVerdicts.Refuse, why)], ReviewRounds = [Round(draft.SpecId)],
    };

    private static ProposalReviewRound Round(string specId) =>
        new(specId, 1, SpecReviewVerdicts.Refuse, [SpecReviewVerdicts.Refuse], [], [], []);

    private static SpecDraft Draft(string id, string yaml) => new(id, $"goal of {id}", yaml, []);
}
