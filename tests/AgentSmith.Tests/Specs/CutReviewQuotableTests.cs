using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using FluentAssertions;
using static AgentSmith.Tests.Specs.CutReviewTestDoubles;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-15-a5a5: admission accepts a verbatim quote from the done-list, or from the goal or
/// a step action of a spec that states none — under a floor a blank string and a bare step id
/// cannot satisfy.
/// </summary>
public sealed class CutReviewQuotableTests
{
    [Fact]
    public async Task Finding_QuotingAGoalOfADoneLessSpec_IsKept()
    {
        var review = await Review([DoneLess()], Uncheckable("e1", "every sender onto the new bus"));

        review.Findings.Should().ContainSingle();
    }

    [Fact]
    public async Task Finding_QuotingAStepActionOfADoneLessSpec_IsKept()
    {
        var review = await Review([DoneLess()], Uncheckable("e1", "Remove the legacy client package"));

        review.Findings.Should().ContainSingle();
    }

    [Theory]
    [InlineData("verify")]
    [InlineData("we verify that the moon is a balloon")]
    public async Task Finding_QuotingABareStepId_IsDiscarded(string quote)
    {
        var draft = DoneLess() with { Steps = [new SpecStep("verify", "verify", null)] };

        var review = await Review([draft], Uncheckable("e1", quote));

        review.Deliverable.Should().BeTrue("a step read back as its bare id identifies nothing");
    }

    [Fact]
    public async Task Finding_AgainstASpecWithABlankDoneEntry_IsDiscarded()
    {
        var draft = new SpecDraft("p1a", "goal", "spec: p1a", []) { Done = ["every sender uses the new bus", ""] };

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
        var draft = new SpecDraft("p1a", "goal", "spec: p1a", []) { Done = ["every sender uses the new bus"] };

        var review = await Review([draft], Uncheckable("p1a", "every sender uses the new bus"));

        review.Findings.Should().ContainSingle();
        SpecQuotableText.Of(DoneLess()).Should().Equal(
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
    public async Task Finding_QuotingTheGoalOfASpecWithCriteria_IsKept()
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

        review.Findings.Should().ContainSingle().Which.SpecId.Should().Be("e1");
        provider.Prompts.Single().Should().StartWith("A draft has been cut into specs, with no ticket behind it.");
    }

    /// <summary>
    /// 2026-10-12-967ad: the prompt asks for "spec_id", and an answer in that key keeps the id of
    /// every finding — the reader still takes "phase_id" from a model that answers the old way.
    /// </summary>
    [Fact]
    public async Task SpecCutReview_SpecIdAnswer_KeepsFindingIds()
    {
        var answer = $$"""[{"spec_id":"e1","criterion":"Remove the legacy client package","problem":"uncheckable","why":"no command shows it"}]""";
        var provider = new LookingProvider(answer);

        var review = await Reviewer(new CappingFactory(provider)).ReviewAsync(
            [DoneLess()], Key, "the ticket", look: null, new AgentConfig(), Tracker(), CancellationToken.None);

        provider.Prompts.Single().Should().Contain("[{\"spec_id\": \"<id>\"")
            .And.NotContain("phase_id", "the prompt and the rendered specs name the id the same way");
        review.Findings.Should().ContainSingle().Which.SpecId.Should().Be("e1");
    }

    private static async Task<SpecCutReview> Review(IReadOnlyList<SpecDraft> drafts, string answer) =>
        await Reviewer(new CappingFactory(new LookingProvider(answer))).ReviewAsync(
            drafts, Key, "the ticket", look: null, new AgentConfig(), Tracker(), CancellationToken.None);

    private static string Uncheckable(string spec, string quote) =>
        $$"""[{"phase_id":"{{spec}}","criterion":"{{quote}}","problem":"uncheckable","why":"no command shows it"}]""";

    private static SpecDraft Criterion(string done) =>
        new("p1a", "goal", "spec: p1a", []) { Done = [done] };

    private static SpecDraft DoneLess() => CutReviewWithoutTicketTests.DoneLess();
}
