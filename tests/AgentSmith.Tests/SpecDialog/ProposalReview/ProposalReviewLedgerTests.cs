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
    public void BuildWithCuts_ThenEdited_StillCovered()
    {
        var ledger = ProposalReviewLedger.Of(new PipelineContext());
        ledger.Record(Passed(Draft("a", "spec: a"), SpecReviewVerdicts.BuildWithCuts));

        ledger.Covers(new SpecOutcome(Draft("a", "spec: a, cuts applied"))).Should().BeTrue();
        ledger.Settled("a").Should().NotBeNull();
    }

    [Fact]
    public void Refused_ThenEdited_NotCovered()
    {
        var ledger = ProposalReviewLedger.Of(new PipelineContext());
        ledger.Record(Reviewed(Draft("a", "spec: a"), "objection"));

        ledger.Covers(new SpecOutcome(Draft("a", "spec: a, rewritten"))).Should().BeFalse();
        ledger.Settled("a").Should().BeNull();
    }

    [Fact]
    public void Compose_SettledEdited_CarriesSettledRounds()
    {
        var ledger = ProposalReviewLedger.Of(new PipelineContext());
        var passed = Passed(Draft("a", "spec: a"), SpecReviewVerdicts.BuildWithCuts) with
        {
            Findings = [new ProposalFinding("a", SpecReviewVerdicts.BuildWithCuts, "cut this"), new ProposalFinding("a", "evidence", "a path")],
        };
        ledger.Record(passed);

        var composed = ledger.Compose(new SpecOutcome(Draft("a", "spec: a, cuts applied")));

        composed.ReviewRounds.Should().ContainSingle().Which.Should().BeSameAs(passed.ReviewRounds[0]);
        composed.Findings.Select(f => f.Why).Should().Equal("a path");
    }

    [Fact]
    public void Compose_SettledEdited_NoReviewedCleanForNewText()
    {
        var ledger = ProposalReviewLedger.Of(new PipelineContext());
        ledger.Record(Passed(Draft("a", "spec: a"), SpecReviewVerdicts.Build) with
        {
            ReviewedClean = [Contracts.Specs.ReviewedText.Of("a", "spec: a")],
        });

        var composed = ledger.Compose(new SpecOutcome(Draft("a", "spec: a, edited")));

        composed.ReviewedClean.Should().BeEmpty();
        composed.NotReviewed.Should().BeNull();
    }

    [Fact]
    public void TicketList_ThenEdited_StillCovered()
    {
        var ledger = ProposalReviewLedger.Of(new PipelineContext());
        ledger.Record(new TicketGraphOutcome([Ticket("order-api")]) with
        {
            Findings = [new ProposalFinding("order-api", "scope", "too wide")],
        });

        var edited = new TicketGraphOutcome([Ticket("order-api"), Ticket("order-ui")]);

        ledger.Covers(edited).Should().BeTrue();
        ledger.Compose(edited).Findings.Should().BeEmpty("the master answered the plan review");
    }

    [Fact]
    public void TicketList_ReviewerFailed_ThenEdited_KeepsNotReviewed()
    {
        var ledger = ProposalReviewLedger.Of(new PipelineContext());
        ledger.Record(new TicketGraphOutcome([Ticket("order-api")]) with
        {
            Findings = [new ProposalFinding(TicketPlanReviewPrompt.ListKey, ProposalReviewSummary.NotReviewedYet, TicketPlanReview.NoAnswerWhy)],
        });

        var composed = ledger.Compose(new TicketGraphOutcome([Ticket("order-api"), Ticket("order-ui")]));

        composed.Findings.Should().ContainSingle().Which.Problem.Should().Be(ProposalReviewSummary.NotReviewedYet);
    }

    [Fact]
    public void ParentNoContract_ThenEdited_StillCovered()
    {
        var ledger = ProposalReviewLedger.Of(new PipelineContext());
        var child = Draft("c", "spec: c");
        ledger.Record(new EpicOutcome(Draft("p", "spec: p"), [child]) with { ReviewRounds = [Pass("c", SpecReviewVerdicts.Build)] });

        ledger.Covers(new EpicOutcome(Draft("p", "spec: p, edited"), [child])).Should().BeTrue();
    }

    [Fact]
    public void Rounds_EveryRecordedRound()
    {
        var ledger = ProposalReviewLedger.Of(new PipelineContext());
        var reviewed = Reviewed(Draft("a", "spec: a"), "objection");
        ledger.Record(reviewed);

        ledger.Rounds().Should().Contain(reviewed.ReviewRounds[0]);
    }

    /// <summary>2026-11-13-b3a2: a draft refused on owner decisions only is settled, and its decision outlives an edit.</summary>
    [Fact]
    public void Compose_DecisionSettledEdited_KeepsDecision()
    {
        var ledger = ProposalReviewLedger.Of(new PipelineContext());
        var decision = new SpecReviewFinding("the policy is only proposed", "ref/policy.md:3 [A1]", "Is the policy approved?");
        ledger.Record(new SpecOutcome(Draft("a", "spec: a")) with
        {
            Findings = [new ProposalFinding("a", SpecReviewVerdicts.Refuse, decision.Text, null, decision.Evidence, decision.Decision)],
            ReviewRounds = [new ProposalReviewRound("a", 1, SpecReviewVerdicts.Refuse, [SpecReviewVerdicts.Refuse], [], [decision], [])],
        });

        var edited = new SpecOutcome(Draft("a", "spec: a, edited"));

        ledger.Covers(edited).Should().BeTrue("a draft waiting on the owner is settled for the turn");
        ledger.Compose(edited).Findings.Should().ContainSingle().Which.Decision.Should().Be("Is the policy approved?");
    }

    private static OutcomeProposal Reviewed(SpecDraft draft, string why) => new SpecOutcome(draft) with
    {
        Findings = [new ProposalFinding(draft.SpecId, SpecReviewVerdicts.Refuse, why)], ReviewRounds = [Round(draft.SpecId)],
    };

    private static OutcomeProposal Passed(SpecDraft draft, string verdict) =>
        new SpecOutcome(draft) with { ReviewRounds = [Pass(draft.SpecId, verdict)] };

    private static ProposalReviewRound Pass(string specId, string verdict) => new(specId, 1, verdict, [verdict], [], [], []);

    private static GraphTicket Ticket(string key) => new(key, $"Title of {key}", $"The scope of {key}.", ["done"], [], ["api"]);

    private static ProposalReviewRound Round(string specId) =>
        new(specId, 1, SpecReviewVerdicts.Refuse, [SpecReviewVerdicts.Refuse], [], [], []);

    private static SpecDraft Draft(string id, string yaml) => new(id, $"goal of {id}", yaml, []);
}
