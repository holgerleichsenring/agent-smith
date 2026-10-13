using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using FluentAssertions;
using static AgentSmith.Tests.Specs.CutReviewTestDoubles;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// A done entry that measures the code's form is a finding of its own: offered always and
/// last, exempt from the style sentence, admitted only when it quotes a done entry.
/// </summary>
public sealed class CutReviewShapeRuleTests
{
    private const string Shape = "every method is at most 20 lines";

    [Theory]
    [InlineData(CutReviewTicket.Whole, true)]
    [InlineData(CutReviewTicket.Whole, false)]
    [InlineData(CutReviewTicket.Truncated, true)]
    [InlineData(CutReviewTicket.Absent, false)]
    public void SpecCutVerdicts_Offered_EndsWithShapeRule(CutReviewTicket ticket, bool canLook)
    {
        SpecCutVerdicts.Offered(ticket, canLook)[^1].Should().Be(SpecCutVerdicts.ShapeRule);
        SpecCutVerdicts.Count(5).Should().Be("five");
    }

    [Fact]
    public void SpecCutReviewPrompt_StyleSentenceExemptsShapeRule()
    {
        var prompt = SpecCutReviewPrompt.For([WithDone()], null, look: null);

        prompt.Should().Contain("Say nothing about style, ordering or how many specs there are — except a shape\n"
            + "rule stated as a done entry, which is a way above.")
            .And.Contain("3. SHAPE RULE");
    }

    [Fact]
    public async Task SpecCutAdmission_ShapeRuleQuotingTheGoal_IsDiscarded()
    {
        var draft = WithDone() with { Goal = "keep every method at most 20 lines" };

        var review = await Review([draft], ShapeRule("p1a", "keep every method at most 20 lines"));

        review.Deliverable.Should().BeTrue("a goal naming a form is work to do, not a verdict");
    }

    [Fact]
    public async Task SpecCutAdmission_ShapeRuleQuotingAStep_IsDiscarded()
    {
        var draft = CutReviewWithoutTicketTests.DoneLess();

        var review = await Review([draft], ShapeRule("e1", "Remove the legacy client package"));

        review.Deliverable.Should().BeTrue("a spec with no done list has nothing a shape rule may quote");
    }

    [Fact]
    public async Task SpecCutAdmission_ShapeRuleQuotingADoneEntry_IsAdmitted()
    {
        var review = await Review([WithDone()], ShapeRule("p1a", Shape));

        review.Findings.Should().ContainSingle().Which.Problem.Should().Be(SpecCutVerdicts.ShapeRule);
    }

    private static async Task<SpecCutReview> Review(IReadOnlyList<SpecDraft> drafts, string answer) =>
        await Reviewer(new CappingFactory(new LookingProvider(answer))).ReviewAsync(
            drafts, Key, "the ticket", look: null, new AgentConfig(), Tracker(), CancellationToken.None);

    private static string ShapeRule(string spec, string quote) =>
        $$"""[{"spec_id":"{{spec}}","criterion":"{{quote}}","problem":"shape-rule","why":"a size is not an outcome"}]""";

    private static SpecDraft WithDone() =>
        new("p1a", "migrate the senders", "spec: p1a", []) { Done = ["every sender uses the new bus", Shape] };
}
