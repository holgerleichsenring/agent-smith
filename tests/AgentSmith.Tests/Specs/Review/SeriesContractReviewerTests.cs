using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using static AgentSmith.Tests.Specs.Review.SeriesReviewFixtures;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-10-13-8762k: the series reviewer's one call and what admission keeps of it, on the member each cut
/// names.</summary>
public sealed class SeriesContractReviewerTests
{
    [Fact]
    public async Task SeriesContractReviewer_RunCutOnItsMemberWithOwnRead_IsKeptNamingTheMember()
    {
        var look = AttackReviewFixtures.Look();
        var id = AttackReviewFixtures.Read(look, SpecFile, lines: 40);

        var review = await ReviewAsync(Answer("BUILD WITH CUTS", Cut(A, "goal", AQuote, id)), look);

        review.Dropped.Should().BeEmpty();
        review.Result!.Verdict.Should().Be(SpecReviewVerdicts.BuildWithCuts);
        review.Result.Cuts.Should().ContainSingle().Which.Should().Match<SpecReviewCut>(
            c => c.SpecId == A && c.Quote == AQuote && c.Evidence == $"shop/{SpecFile}:3 [{id}]");
    }

    [Fact]
    public async Task SeriesContractReviewer_CutQuotingTextNotInItsMember_IsDroppedAsEvidenceProblem()
    {
        var look = AttackReviewFixtures.Look();
        var id = AttackReviewFixtures.Read(look, SpecFile, lines: 40);

        var review = await ReviewAsync(Answer("BUILD WITH CUTS", Cut(B, "goal", AQuote, id)), look);

        review.Result!.Cuts.Should().BeEmpty("the quote is A's goal, not B's");
        review.Result.Verdict.Should().Be(SpecReviewVerdicts.Build);
        review.Dropped.Should().ContainSingle().Which.Problem.Should().Contain($"not a run of the register 'goal' of {B}");
    }

    [Fact]
    public async Task SeriesContractReviewer_CutQuotingRequires_IsDroppedAsEvidenceProblem()
    {
        var look = AttackReviewFixtures.Look();
        var id = AttackReviewFixtures.Read(look, SpecFile, lines: 40);

        var review = await ReviewAsync(Answer("BUILD WITH CUTS", Cut(A, "requires", "2026-10-14-1234a", id)), look);

        review.Result!.Cuts.Should().BeEmpty("the reviser keeps requires byte-identical");
        review.Dropped.Should().ContainSingle().Which.Problem.Should().Contain("register 'requires'");
    }

    [Fact]
    public async Task SeriesContractReviewer_RunCutWithoutOwnRead_IsDropped()
    {
        var look = AttackReviewFixtures.Look();

        var review = await ReviewAsync(Answer("BUILD WITH CUTS", Cut(A, "goal", AQuote, "A9")), look);

        review.Result!.Cuts.Should().BeEmpty();
        review.Dropped.Should().ContainSingle().Which.Problem.Should().Contain("no read of its own");
    }

    [Fact]
    public async Task SeriesContractReviewer_CutNamingNoMember_IsDropped()
    {
        var look = AttackReviewFixtures.Look();
        var id = AttackReviewFixtures.Read(look, SpecFile, lines: 40);

        var review = await ReviewAsync(
            Answer("BUILD WITH CUTS", $"{Cut(null, "goal", AQuote, id)}, {Cut("2026-01-01-0000a", "goal", AQuote, id)}"), look);

        review.Result!.Cuts.Should().BeEmpty();
        review.Dropped.Should().HaveCount(2).And.OnlyContain(p => p.Problem == "it names no member of the series");
    }

    [Fact]
    public async Task SeriesContractReviewer_TurnCutQuotingAContractEntry_IsKeptWithTheEntryAsEvidence()
    {
        var cut = $$"""{"spec_id": "{{A}}", "register": "goal", "quote": "{{AQuote}}", "replacement": null, "entry": "event 'OrderExported' = 87"}""";

        var review = await ReviewAsync(Answer("BUILD WITH CUTS", cut), look: null, Input(inRun: false));

        review.Result!.Cuts.Should().ContainSingle().Which.Evidence
            .Should().Be($"series contract: name: event 'OrderExported' = 87, owner {A}");
    }

    [Fact]
    public async Task SeriesContractReviewer_CallFails_RoundNotTaken()
    {
        var factory = new ScriptedChatClientFactory(() => throw new TaskCanceledException("provider timeout"));

        var review = await Reviewer(factory).ReviewAsync(Input(), AttackReviewFixtures.Look(), new AgentConfig(),
            PipelineCostTracker.GetOrCreate(new PipelineContext()), CancellationToken.None);

        review.Result.Should().BeNull();
        review.NotTaken.Should().Contain("call failed");
    }

    [Fact]
    public async Task SeriesContractReviewer_RunWithoutLook_IsNotTakenAndMakesNoCall()
    {
        var factory = new ScriptedChatClientFactory();

        var review = await Reviewer(factory).ReviewAsync(Input(), look: null, new AgentConfig(),
            PipelineCostTracker.GetOrCreate(new PipelineContext()), CancellationToken.None);

        review.NotTaken.Should().Contain("no repository");
        factory.ResponseCounter.Should().BeEmpty();
    }

    private static Task<SeriesContractReview> ReviewAsync(string answer, DerivationLook? look, SeriesContractInput? input = null) =>
        Reviewer(new ScriptedChatClientFactory(() => new ChatResponse(new ChatMessage(ChatRole.Assistant, answer))))
            .ReviewAsync(input ?? Input(), look, new AgentConfig(), PipelineCostTracker.GetOrCreate(new PipelineContext()),
                CancellationToken.None);
}
