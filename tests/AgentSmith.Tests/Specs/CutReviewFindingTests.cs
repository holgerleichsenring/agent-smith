using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using FluentAssertions;
using static AgentSmith.Tests.Specs.CutReviewTestDoubles;
using static AgentSmith.Tests.Specs.DerivationTestLooks;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-15-ffa7: admission has two shapes. A false premise is kept only when it cites an
/// id the framework minted for a look THIS reviewer took; every other kind keeps the quote.
/// </summary>
public sealed class CutReviewFindingTests
{
    private const string Criterion = "every sender uses the new bus";

    [Fact]
    public async Task Finding_FalsePremiseCitingAMintedId_IsKept()
    {
        var review = await ReviewAfterOneLook(FalsePremise("\"R1\""));

        review.Findings.Should().ContainSingle().Which.Cites.Should().Be("R1");
    }

    [Fact]
    public async Task Finding_FalsePremiseCitingAnIdNobodyMinted_IsDiscarded()
    {
        var review = await ReviewAfterOneLook(FalsePremise("\"R9\""));

        review.Deliverable.Should().BeTrue("an id the framework never minted proves nothing");
    }

    [Fact]
    public async Task Finding_FalsePremiseWithNoCitation_IsDiscarded()
    {
        var review = await ReviewAfterOneLook(FalsePremise("null"));

        review.Deliverable.Should().BeTrue("the same statement without a minted id changes nothing");
    }

    [Fact]
    public async Task Finding_FalsePremiseCitingTheDeriversSpelling_IsDiscarded()
    {
        var review = await ReviewAfterOneLook(FalsePremise("\"L1\""));

        review.Deliverable.Should().BeTrue("L1 names a look of the deriver's, not the reviewer's");
    }

    [Fact]
    public async Task Finding_FalsePremiseCitingALookThatCouldNotRun_IsDiscarded()
    {
        var review = await ReviewAfterOneLook(FalsePremise("\"R1\""), exitCode: 2);

        review.Deliverable.Should().BeTrue("a search that could not run proves nothing, id or not");
    }

    [Theory]
    [InlineData("[\"R9\", \"R1\"]")]
    [InlineData("\"R9, R1\"")]
    public async Task Finding_FalsePremiseCitingSeveralIds_IsKeptOnTheFirstThatResolves(string cites)
    {
        var review = await ReviewAfterOneLook(FalsePremise(cites));

        review.Findings.Should().ContainSingle().Which.Cites.Should().Contain("R1");
    }

    [Fact]
    public async Task Finding_ContradictionQuotingACriterion_IsKeptAsBefore()
    {
        var review = await ReviewAfterOneLook($$"""
            [{"phase_id":"p1a","criterion":"{{Criterion}}","problem":"contradiction",
              "why":"cannot both hold","conflicts_with":null,"cites":null}]
            """);

        review.Findings.Should().ContainSingle().Which.Problem.Should().Be(SpecCutVerdicts.Contradiction);
    }

    private static string FalsePremise(string cites) => $$"""
        [{"phase_id":"p1a","criterion":"the senders already reference the new bus",
          "problem":"false-premise","why":"the search finds no reference to it",
          "conflicts_with":null,"cites":{{cites}}}]
        """;

    private static async Task<SpecCutReview> ReviewAfterOneLook(string answer, int exitCode = 1)
    {
        var look = CutReviewLookTests.ReviewLook(new CountingSandbox(exitCode));
        var review = await Reviewer(new CappingFactory(new LookingProvider(answer, "NewBus"))).ReviewAsync(
            Drafts(Criterion), Key, "the ticket", look, new AgentConfig(), Tracker(), CancellationToken.None);
        look.Evidence.Lines.Should().ContainSingle().Which.Should().StartWith("[R1] ");
        return review;
    }
}
