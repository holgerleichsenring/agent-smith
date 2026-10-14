using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Events;
using FluentAssertions;
using static AgentSmith.Tests.Specs.Review.CutRevisionFixtures;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-10-13-8762f: the reviser is told every cut, every problem and how to match a quote.</summary>
public sealed class SpecCutRevisionPromptTests
{
    [Fact]
    public void SpecCutRevisionPrompt_CarriesEveryCutAndProblem()
    {
        SpecReviewCut[] cuts =
        [
            Cut(SpecRegisterNames.Done, "flushes after every single order", "flushes once per batch"),
            Cut(SpecRegisterNames.Steps, "through the buffered file writer"),
        ];

        var prompt = SpecCutRevisionPrompt.For(Draft(), cuts, [Problem()], lastRound: false);

        prompt.Should().Contain("\"flushes after every single order\"").And.Contain("\"flushes once per batch\"")
            .And.Contain("\"through the buffered file writer\"").And.Contain("remove the quoted words")
            .And.Contain("shop/src/Export/Writer.cs:9 [A1]")
            .And.Contain("The order store pages its reads by one hundred.: src/Orders/OrderStore.cs has 31 lines")
            .And.Contain("Leave every fact listed under evidence problems in place")
            .And.Contain("spec: 2026-10-14-5678a");
        SpecCutRevisionPrompt.For(Draft(), cuts, [Problem()], lastRound: true)
            .Should().Contain("into\n  assumptions, written as its claim exactly");
    }

    [Fact]
    public void SpecCutRevisionPrompt_SaysAQuoteMaySpanAFoldedLine() =>
        SpecCutRevisionPrompt.For(Draft(), [], [], lastRound: false)
            .Should().Contain("A quote may span a folded line").And.Contain("match a quote by its words");
}
