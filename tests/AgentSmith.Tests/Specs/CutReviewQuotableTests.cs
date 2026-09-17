using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using FluentAssertions;
using static AgentSmith.Tests.Specs.CutReviewTestDoubles;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-15-a5a5: admission accepts a verbatim quote from the done-list, or from the goal or
/// a step action of a phase that states none — under a floor a blank string and a bare step id
/// cannot satisfy.
/// </summary>
public sealed class CutReviewQuotableTests
{
    [Fact]
    public async Task Finding_QuotingAGoalOfADoneLessPhase_IsKept()
    {
        var review = await Review([DoneLess()], Uncheckable("e1", "every sender onto the new bus"));

        review.Findings.Should().ContainSingle();
    }

    [Fact]
    public async Task Finding_QuotingAStepActionOfADoneLessPhase_IsKept()
    {
        var review = await Review([DoneLess()], Uncheckable("e1", "Remove the legacy client package"));

        review.Findings.Should().ContainSingle();
    }

    [Theory]
    [InlineData("verify")]
    [InlineData("we verify that the moon is a balloon")]
    public async Task Finding_QuotingABareStepId_IsDiscarded(string quote)
    {
        var draft = DoneLess() with { Steps = [new PhaseStep("verify", "verify", null)] };

        var review = await Review([draft], Uncheckable("e1", quote));

        review.Deliverable.Should().BeTrue("a step read back as its bare id identifies nothing");
    }

    [Fact]
    public async Task Finding_AgainstAPhaseWithABlankDoneEntry_IsDiscarded()
    {
        var draft = new PhaseDraft("p1a", "goal", "phase: p1a", []) { Done = ["every sender uses the new bus", ""] };

        var review = await Review([draft], Uncheckable("p1a", "the moon is a balloon"));

        review.Deliverable.Should().BeTrue("a blank entry is contained in everything and admitted every finding");
    }

    [Fact]
    public async Task Finding_QuotingNothingTheDraftStates_IsStillDiscarded()
    {
        var review = await Review([DoneLess()], Uncheckable("e1", "the moon is a balloon"));

        review.Deliverable.Should().BeTrue();
    }

    [Fact]
    public async Task Finding_QuotingACriterion_IsKeptAsBefore()
    {
        var draft = new PhaseDraft("p1a", "goal", "phase: p1a", []) { Done = ["every sender uses the new bus"] };

        var review = await Review([draft], Uncheckable("p1a", "every sender uses the new bus"));

        review.Findings.Should().ContainSingle();
        PhaseQuotableText.Of(DoneLess()).Should().Equal(
            "Move every sender onto the new bus",
            "Replace the sender registration in the host", "Remove the legacy client package");
    }

    [Theory]
    [InlineData("the new")]
    [InlineData("uses the")]
    [InlineData("r uses")]
    public async Task Finding_QuotingAFragmentOfACriterion_IsDiscarded(string quote)
    {
        var review = await Review([Criterion("every sender uses the new bus")], Uncheckable("p1a", quote));

        review.Deliverable.Should().BeTrue("a few words of a criterion do not say which criterion");
    }

    [Fact]
    public async Task Finding_QuotingMostOfACriterion_IsKept()
    {
        var review = await Review([Criterion("every sender uses the new bus")], Uncheckable("p1a", "Sender uses the new bus."));

        review.Findings.Should().ContainSingle();
    }

    [Fact]
    public async Task Finding_InventedSentenceContainingAShortCriterion_IsDiscarded()
    {
        var review = await Review([Criterion("tests pass")], Uncheckable("p1a", "the moon is a balloon so tests pass"));

        review.Deliverable.Should().BeTrue("a sentence that contains a criterion is not a quote of it");
    }

    [Theory]
    [InlineData("Builds")]
    [InlineData("builds.")]
    public async Task Finding_QuotingAOneWordCriterion_IsKept(string quote)
    {
        var review = await Review([Criterion("Builds")], Uncheckable("p1a", quote));

        review.Findings.Should().ContainSingle("a whole criterion is a quote whatever its length");
    }

    [Fact]
    public async Task Finding_QuotingTheGoalOfAPhaseWithCriteria_IsKept()
    {
        var draft = Criterion("every sender uses the new bus") with { Goal = "keep the legacy client everywhere" };

        var review = await Review([draft], Uncheckable("p1a", "keep the legacy client everywhere"));

        review.Findings.Should().ContainSingle("a goal can contradict its own criteria");
    }

    [Fact]
    public async Task Review_CalledWithDraftsAlone_NeedsNoFabricatedSpecSet()
    {
        var provider = new LookingProvider(Uncheckable("e1", "Remove the legacy client package"));

        var review = await Reviewer(new CappingFactory(provider)).ReviewAsync(
            [DoneLess()], "dialog-7", ticketText: null, look: null, new AgentConfig(), Tracker(), CancellationToken.None);

        review.Findings.Should().ContainSingle().Which.PhaseId.Should().Be("e1");
        provider.Prompts.Single().Should().StartWith("A draft has been cut into phases, with no ticket behind it.");
    }

    private static async Task<SpecCutReview> Review(IReadOnlyList<PhaseDraft> drafts, string answer) =>
        await Reviewer(new CappingFactory(new LookingProvider(answer))).ReviewAsync(
            drafts, Key, "the ticket", look: null, new AgentConfig(), Tracker(), CancellationToken.None);

    private static string Uncheckable(string phase, string quote) =>
        $$"""[{"phase_id":"{{phase}}","criterion":"{{quote}}","problem":"uncheckable","why":"no command shows it"}]""";

    private static PhaseDraft Criterion(string done) =>
        new("p1a", "goal", "phase: p1a", []) { Done = [done] };

    private static PhaseDraft DoneLess() => CutReviewWithoutTicketTests.DoneLess();
}
