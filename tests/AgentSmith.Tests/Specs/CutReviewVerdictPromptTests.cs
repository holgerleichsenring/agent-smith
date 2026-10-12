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
    public void Prompt_WithoutALook_OffersFourVerdictsAndNoCitesField()
    {
        var prompt = SpecCutReviewPrompt.For(Drafts("a criterion"), "the ticket", look: null);

        prompt.Should().Contain("There are four ways:")
            .And.Contain("3. NOT IN THE TICKET")
            .And.Contain("4. SHAPE RULE")
            .And.Contain("\"problem\": \"contradiction|uncheckable|not-in-ticket|shape-rule\"")
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
            .And.NotContain("NOT IN THE TICKET");
    }

    [Fact]
    public void Prompt_WithALookAndAWholeTicket_OffersFiveVerdicts()
    {
        var look = CutReviewLookTests.ReviewLook(new CountingSandbox(0));

        var prompt = SpecCutReviewPrompt.For(Drafts("a criterion"), "the ticket", look);

        prompt.Should().Contain("There are five ways:").And.Contain("4. FALSE PREMISE")
            .And.Contain("5. SHAPE RULE");
        SpecCutVerdicts.Offered(CutReviewTicket.Whole, canLook: true).Should().Equal(
            SpecCutVerdicts.Contradiction, SpecCutVerdicts.Uncheckable,
            SpecCutVerdicts.NotInTicket, SpecCutVerdicts.FalsePremise, SpecCutVerdicts.ShapeRule);
    }
}
