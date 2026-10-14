using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Events;
using FluentAssertions;
using static AgentSmith.Tests.Specs.Review.CutRevisionFixtures;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-10-13-8762f: what a revision left standing is named with its register.</summary>
public sealed class SpecCutResiduesTests
{
    private const string Flush = "The writer flushes after every single order.";

    [Fact]
    public void SpecCutResidues_QuoteStillInAnotherRegister_IsAResidueNamingThatRegister()
    {
        var cut = Cut(SpecRegisterNames.Done, "flushes after every single order", "flushes once per batch");
        var revised = Draft(With(Flush, "The writer flushes once per batch.",
            With("keeps the archive readable by hand.", "keeps the archive readable; the writer flushes after every single order.")));

        new SpecCutResidues().Of(revised, [cut], []).Should().Equal(
            new SpecReviewResidue(SpecRegisterNames.Decisions, "flushes after every single order"));
    }

    [Fact]
    public void SpecCutResidues_ReplacementContainingTheQuote_IsNotAResidue()
    {
        var cut = Cut(SpecRegisterNames.Done, "flushes after every single order",
            "flushes after every single order and logs it");
        var revised = Draft(With(Flush, "The writer flushes after every single order and logs it."));

        new SpecCutResidues().Of(revised, [cut], []).Should().BeEmpty();
    }

    [Fact]
    public void SpecCutResidues_FactWithAProblemStillInFactsWithItsEvidence_IsAResidue() =>
        new SpecCutResidues().Of(Draft(), [], [Problem()]).Should().Equal(
            new SpecReviewResidue(SpecRegisterNames.Facts, "The order store pages its reads by one hundred."));

    [Fact]
    public void SpecCutResidues_ReCitedFact_IsNotAResidue()
    {
        var cut = Cut(SpecRegisterNames.Facts, "src/Orders/OrderStore.cs:40", "src/Orders/OrderPager.cs:12");
        var revised = Draft(With("src/Orders/OrderStore.cs:40", "src/Orders/OrderPager.cs:12"));

        new SpecCutResidues().Of(revised, [cut], [Problem()]).Should().BeEmpty();
    }

    [Fact]
    public void SpecCutResidues_RefusedRevision_EveryCutIsAResidue()
    {
        SpecReviewCut[] cuts =
        [
            Cut(SpecRegisterNames.Done, "flushes after every single order", "flushes once per batch"),
            Cut(SpecRegisterNames.Steps, "through the buffered file writer"),
        ];

        new SpecCutResidues().Of(Draft(), cuts, []).Should().Equal(
            new SpecReviewResidue(SpecRegisterNames.Done, "flushes after every single order"),
            new SpecReviewResidue(SpecRegisterNames.Steps, "through the buffered file writer"));
    }
}
