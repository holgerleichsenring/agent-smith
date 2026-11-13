using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>2026-11-12-92e9b / 2026-11-13-3f79: the review report names only what the pass itself took.</summary>
public sealed class DesignTurnReviewReportTests
{
    [Fact]
    public void For_NothingReviewed_IsNull_ReviewedFindings_AreReported()
    {
        var capped = new SpecOutcome(Draft("a")) with
        {
            Findings = [new ProposalFinding("a", ProposalReviewSummary.NotReviewedYet, "cost cap")], NotReviewed = ["a"],
        };

        DesignTurnReviewReport.For(capped, []).Should().BeNull();
        DesignTurnReviewReport.For(Refused("a", "objection"), []).Should()
            .StartWith(DesignTurnReviewReport.Heading).And.Contain($"a: {SpecReviewVerdicts.Refuse}").And.Contain("objection");
    }

    [Fact]
    public void CarriedRounds_NotReported()
    {
        var carried = Refused("a", "settled objection");
        var fresh = Refused("b", "fresh objection");
        var reviewed = new EpicOutcome(Draft("p"), [Draft("a"), Draft("b")]) with
        {
            ReviewRounds = [.. carried.ReviewRounds, .. fresh.ReviewRounds], Findings = [.. carried.Findings, .. fresh.Findings],
        };

        var report = DesignTurnReviewReport.For(reviewed, carried.ReviewRounds);

        report.Should().Contain("fresh objection").And.Contain($"b: {SpecReviewVerdicts.Refuse}");
        report.Should().NotContain("settled objection").And.NotContain($"- a: {SpecReviewVerdicts.Refuse}");
        DesignTurnReviewReport.For(carried, carried.ReviewRounds).Should().BeNull("every round was carried");
    }

    [Fact]
    public void ListPass_NothingFound_ReportsClean()
    {
        var report = DesignTurnReviewReport.For(new TicketGraphOutcome([Ticket("order-api")]), []);

        report.Should().StartWith(DesignTurnReviewReport.Heading).And.Contain(DesignTurnReviewReport.CleanList);
    }

    [Fact]
    public void ListPass_ReviewerFailed_ReportsNotReviewedLine_NoCleanLine()
    {
        var failed = new TicketGraphOutcome([Ticket("order-api")]) with
        {
            Findings = [new ProposalFinding(TicketPlanReviewPrompt.ListKey, ProposalReviewSummary.NotReviewedYet, TicketPlanReview.NoAnswerWhy)],
        };

        var report = DesignTurnReviewReport.For(failed, []);

        report.Should().Contain($"[{ProposalReviewSummary.NotReviewedYet}]: {TicketPlanReview.NoAnswerWhy}")
            .And.NotContain(DesignTurnReviewReport.CleanList);
    }

    [Fact]
    public void ListPass_Bound_NoReport()
    {
        var bound = new TicketGraphOutcome([Ticket("order-api")]) with
        {
            Findings = [new ProposalFinding(TicketPlanReviewPrompt.ListKey, TicketPlanReview.BoundProblem, TicketPlanReview.BoundWhy)],
        };

        DesignTurnReviewReport.For(bound, []).Should().BeNull();
    }

    /// <summary>2026-11-13-b3a2: a draft refused on owner decisions only is listed apart, each decision as a question.</summary>
    [Fact]
    public void OwnerDecisions_ListedApartAsQuestions()
    {
        var decision = new SpecReviewFinding("the policy is only proposed", "ref/policy.md:3 [A1]", "is the retention policy approved");
        var waiting = new SpecOutcome(Draft("a")) with
        {
            Findings = [new ProposalFinding("a", SpecReviewVerdicts.Refuse, decision.Text, null, decision.Evidence, decision.Decision)],
            ReviewRounds = [new ProposalReviewRound("a", 1, SpecReviewVerdicts.Refuse, [SpecReviewVerdicts.Refuse], [], [decision], [])],
        };
        var defect = Refused("b", "the handler does not exist");
        var reviewed = new EpicOutcome(Draft("p"), [Draft("a"), Draft("b")]) with
        {
            ReviewRounds = [.. waiting.ReviewRounds, .. defect.ReviewRounds], Findings = [.. waiting.Findings, .. defect.Findings],
        };

        var report = DesignTurnReviewReport.For(reviewed, [])!;

        report.Should().Contain(DesignTurnReviewReport.WaitingHeading).And.Contain(DesignTurnReviewReport.WaitingRule)
            .And.Contain("is the retention policy approved?").And.Contain($"b: {SpecReviewVerdicts.Refuse}");
        report.Should().NotContain($"- a: {SpecReviewVerdicts.Refuse}").And.NotContain("- a [");
        report.IndexOf("the handler does not exist", StringComparison.Ordinal).Should()
            .BeLessThan(report.IndexOf(DesignTurnReviewReport.WaitingHeading, StringComparison.Ordinal));
    }

    private static GraphTicket Ticket(string key) => new(key, $"Title of {key}", $"The scope of {key}.", ["done"], [], ["api"]);

    private static OutcomeProposal Refused(string id, string why) => new SpecOutcome(Draft(id)) with
    {
        Findings = [new ProposalFinding(id, SpecReviewVerdicts.Refuse, why)],
        ReviewRounds = [new ProposalReviewRound(id, 1, SpecReviewVerdicts.Refuse, [SpecReviewVerdicts.Refuse], [], [], [])],
    };

    private static SpecDraft Draft(string id) => new(id, $"goal of {id}", $"spec: {id}", []);
}
