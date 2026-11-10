using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Moq;
using static AgentSmith.Tests.Specs.Review.ReviewLoopRig;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>
/// 2026-11-10-8e59: the build-time gate is the one guarantee — the master builds only text a review ended clean on
/// in that exact form, the series contract included; the author's answer to the objection is the one exception.
/// </summary>
public sealed class ExactTextGateTests
{
    private static readonly SpecReviewCut FlushCut = CutRevisionFixtures.Cut(
        SpecRegisterNames.Scope, "The writer flushes after every order", "The writer flushes once per batch");

    [Fact]
    public async Task ReviewLoop_NoReviewerAnswersInAnyRound_IsStoppedLikeTheBudget()
    {
        var rig = new ReviewLoopRig().Answers(null, null);

        var result = await rig.ExecuteAsync(Settings(rounds: 2));

        rig.Inputs.Count(i => i.Draft.SpecId == First).Should().Be(2, "each round tries again");
        var stop = rig.Pipeline.Get<SpecSequenceStop>(ContextKeys.SpecSequenceStop);
        stop.BeforeSpecId.Should().Be(First, "a text no reviewer answered on is not built in this attempt");
        result.Message.Should().Contain($"no reviewer answered on {First}'s text in round 2");
    }

    [Fact]
    public async Task ReviewLoop_NoAnswerThenBuild_IsClearedInTheNextRound()
    {
        var rig = new ReviewLoopRig().Answers(null, Build());

        var outcome = await rig.Loop().RunAsync(rig.Run(), rig.Set.Specs[0], first: true, CancellationToken.None);

        outcome.IsCleared.Should().BeTrue();
        outcome.Rounds.Should().Be(2);
    }

    [Fact]
    public async Task ReviewLoop_LastRoundRefuseRewritten_IsNotClearedAndAsksTheAuthor()
    {
        var rig = new ReviewLoopRig().Answers(Refuse());
        var spec = rig.Set.Specs[0];
        rig.Rederiver.Setup(r => r.RederiveAsync(It.IsAny<PipelineContext>(), It.IsAny<SpecDraft>(),
                It.IsAny<IReadOnlyList<SpecReviewFinding>>(), It.IsAny<string?>(), It.IsAny<AgentConfig>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(spec.Draft with { Yaml = spec.Draft.Yaml.Replace("one file", "one dated file") });

        var outcome = await rig.Loop().RunAsync(rig.Run(rounds: 1), spec, first: true, CancellationToken.None);

        outcome.IsCleared.Should().BeFalse("the rewrite came after the last round, so nobody attacked it");
        outcome.Unattacked.Should().BeTrue();
        StandingObjection.Of(outcome).Should().NotBeNull();
    }

    [Fact]
    public async Task ReviewSpecs_EveryMemberCleanButNotTheContract_AttacksTheContract()
    {
        var set = TwoSpecs() with { Contract = SeriesReviewFixtures.Contract(First) };
        var rig = new ReviewLoopRig(set);
        Approve(rig, set, [.. set.Specs.Select(s => ReviewedText.Of(s.SpecId, s.Draft.Yaml))]);

        await rig.ExecuteAsync(Settings());

        rig.Inputs.Should().BeEmpty("every member's exact text was reviewed clean");
        rig.SeriesCalls.Should().ContainSingle("no review ended clean on this contract");
    }

    [Fact]
    public async Task ReviewSpecs_EveryMemberAndTheContractClean_SkipsTheReview()
    {
        var set = TwoSpecs() with { Contract = SeriesReviewFixtures.Contract(First) };
        var rig = new ReviewLoopRig(set);
        Approve(rig, set, [.. set.Specs.Select(s => ReviewedText.Of(s.SpecId, s.Draft.Yaml)), ReviewedText.Of(set.Contract!)]);

        var result = await rig.ExecuteAsync(Settings());

        result.Message.Should().Contain("spec review skipped");
        rig.SeriesCalls.Should().BeEmpty();
    }

    [Fact]
    public void ReviewedText_Contract_SurvivesTheManifestRoundTrip()
    {
        var contract = SeriesReviewFixtures.Contract(First);

        var read = SeriesContractYaml.Read(SeriesContractYaml.Write(contract))!;

        ReviewedText.Covers([ReviewedText.Of(contract)], read).Should().BeTrue();
    }

    // The one mechanical exception: re-grounding at spec entry moves a citation's path and lines, never a claim.
    [Fact]
    public void SpecFactRegrounding_TokenAlsoInTheClaim_RewritesOnlyTheEvidence()
    {
        const string yaml = """
            spec: x
            facts:
              - claim: "The handler at src/A.cs:12 reads the orders."
                evidence: "src/A.cs:12"
            """;

        var rewritten = SpecFactRegrounding.Apply(yaml, 0, "src/A.cs:12", "src/A.cs:14")!;

        rewritten.Should().Contain("claim: \"The handler at src/A.cs:12 reads the orders.\"")
            .And.Contain("evidence: \"src/A.cs:14\"");
    }

    private static void Approve(ReviewLoopRig rig, SpecSet set, IReadOnlyList<ReviewedText> clean) =>
        rig.Pipeline.Set(ContextKeys.ApprovedSpecSet, SpecApprovalJson.Write(new SpecApprovalRecord("shop-42",
            set with { Approval = new SpecApproval(DateTimeOffset.UtcNow, "session-1", "carol") }, ["shop"])
        {
            ReviewedClean = clean,
        }));
}
