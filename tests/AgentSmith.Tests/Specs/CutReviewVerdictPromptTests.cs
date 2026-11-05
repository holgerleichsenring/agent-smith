using AgentSmith.Application.Services.Specs;
using FluentAssertions;
using static AgentSmith.Tests.Specs.CutReviewTestDoubles;
using static AgentSmith.Tests.Specs.DerivationTestLooks;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-15-ffa7: the verdict list is composed from what is on offer — the coverage verdict
/// needs the whole ticket, the false premise needs a look — never numbered in the template.
/// </summary>
public sealed class CutReviewVerdictPromptTests
{
    [Fact]
    public void Prompt_WithoutALook_OffersFiveVerdictsAndNoCitesField()
    {
        var prompt = SpecCutReviewPrompt.For(Drafts("a criterion"), "the ticket", look: null);

        prompt.Should().Contain("There are five ways:")
            .And.Contain("3. NOT IN THE TICKET")
            .And.Contain("4. DONE DROPPED")
            .And.Contain("5. SHAPE RULE")
            .And.Contain("\"problem\": \"contradiction|uncheckable|not-in-ticket|done-dropped|shape-rule\"")
            .And.NotContain("FALSE PREMISE")
            .And.NotContain("\"cites\"");
    }

    [Fact]
    public void Prompt_WithALookAndATruncatedTicket_OffersContradictionUncheckableAndFalsePremise()
    {
        var look = CutReviewLookTests.ReviewLook(new CountingSandbox(0));

        var prompt = SpecCutReviewPrompt.For(Drafts("a criterion"), new string('x', 400_000), look);

        prompt.Should().Contain("There are four ways:")
            .And.Contain("3. FALSE PREMISE")
            .And.Contain("Judge only the four\nways above")
            .And.Contain("\"problem\": \"contradiction|uncheckable|false-premise|shape-rule\"")
            .And.Contain("\"cites\"")
            .And.NotContain("NOT IN THE TICKET")
            .And.NotContain("DONE DROPPED")
            .And.Contain("\"criterion\": \"<verbatim from the spec>\"")
            .And.Contain("A finding whose quote is not in that spec is discarded:");
    }

    [Fact]
    public void Prompt_WithALookAndAWholeTicket_OffersSixVerdicts()
    {
        var look = CutReviewLookTests.ReviewLook(new CountingSandbox(0));

        var prompt = SpecCutReviewPrompt.For(Drafts("a criterion"), "the ticket", look);

        prompt.Should().Contain("There are six ways:").And.Contain("4. DONE DROPPED")
            .And.Contain("5. FALSE PREMISE").And.Contain("6. SHAPE RULE");
    }

    [Fact]
    public void SpecCutVerdicts_WholeTicket_OffersDoneDropped()
    {
        SpecCutVerdicts.Offered(CutReviewTicket.Whole, canLook: true).Should().Equal(
            SpecCutVerdicts.Contradiction, SpecCutVerdicts.Uncheckable, SpecCutVerdicts.NotInTicket,
            SpecCutVerdicts.DoneDropped, SpecCutVerdicts.FalsePremise, SpecCutVerdicts.ShapeRule);
        SpecCutVerdicts.Offered(CutReviewTicket.Truncated, canLook: true).Should().NotContain(SpecCutVerdicts.DoneDropped);
        SpecCutVerdicts.Offered(CutReviewTicket.Absent, canLook: false).Should().NotContain(SpecCutVerdicts.DoneDropped);
        SpecCutVerdicts.Count(6).Should().Be("six");
    }

    [Fact]
    public void Contradiction_IsBetweenTwoStatementsOfTheSpec()
    {
        var look = CutReviewLookTests.ReviewLook(new CountingSandbox(0));

        var verdicts = SpecCutVerdicts.Describe(
            SpecCutVerdicts.Offered(CutReviewTicket.Whole, canLook: true), CutReviewTicket.Whole);
        var contradiction = verdicts[verdicts.IndexOf("CONTRADICTION", StringComparison.Ordinal)..verdicts.IndexOf("2. ", StringComparison.Ordinal)];

        contradiction.Should().Contain("statements are the spec's own")
            .And.Contain("the ticket's wording or today's code is never one\n   side")
            .And.Contain(DoneTargetState.Rule);
        verdicts[verdicts.IndexOf("FALSE PREMISE", StringComparison.Ordinal)..].Should().Contain(DoneTargetState.Rule);
        SpecCutReviewPrompt.For(Drafts("a criterion"), "the ticket", look).Should().Contain(contradiction);
    }

    [Fact]
    public void NotInTicket_ATitleStatesTheSymptom()
    {
        var verdicts = SpecCutVerdicts.Describe(
            SpecCutVerdicts.Offered(CutReviewTicket.Whole, canLook: false), CutReviewTicket.Whole);
        var notInTicket = verdicts[verdicts.IndexOf("NOT IN THE TICKET", StringComparison.Ordinal)..verdicts.IndexOf("4. ", StringComparison.Ordinal)];

        notInTicket.Should().Contain("title describing a defect states the symptom")
            .And.Contain(DoneTargetState.Rule);
    }

    [Fact]
    public void Prompt_WithAWholeTicket_SaysADoneDroppedQuotesTheTicket()
    {
        var prompt = SpecCutReviewPrompt.For(Drafts("a criterion"), "the ticket", look: null);

        prompt.Should().Contain("A done-dropped instead quotes the ticket's outcome VERBATIM.")
            .And.Contain("not in that spec — or, for a done-dropped, not in the ticket — is discarded:")
            .And.Contain("\"criterion\": \"<verbatim from the spec, or from the ticket for a done-dropped>\"");
    }
}
