using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using FluentAssertions;
using Moq;
using static AgentSmith.Tests.Specs.Review.ReviewLoopRig;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>
/// 2026-10-13-8762g: one spec's rounds — a BUILD stops without a revision, applied cuts with clean evidence
/// stop after their round, a residue earns another round, an unchanged text ends the loop, the last round is
/// told so, a REFUSE without cuts is rewritten once, and every reviewer's answer is stored after the cuts.
/// </summary>
public sealed class SpecReviewLoopTests
{
    private const string FlushScope = "The writer flushes after every order.";
    private const string BatchScope = "The writer flushes once per batch.";
    private static readonly SpecReviewCut FlushCut =
        CutRevisionFixtures.Cut(SpecRegisterNames.Scope, "The writer flushes after every order", "The writer flushes once per batch");
    private static readonly (string, string) GoalOnly =
        ("every order of the day into one file", "every order of the day into one dated file");

    [Fact]
    public async Task ReviewLoop_FirstRoundBuild_StopsWithoutRevision()
    {
        var rig = new ReviewLoopRig().Answers(Build());

        var outcome = await RunAsync(rig);

        outcome.Rounds.Should().Be(1);
        outcome.StandingVerdict.Should().BeNull();
        rig.Published.Should().BeEmpty("a round that changed nothing writes no revision");
        rig.Events.Events.Should().ContainSingle();
    }

    [Fact]
    public async Task ReviewLoop_CutsAppliedAndRevisedTextEvidenceClean_StopsAfterThatRound()
    {
        var rig = new ReviewLoopRig().Answers(Cuts(FlushCut)).Revisions((FlushScope, BatchScope));

        var outcome = await RunAsync(rig);

        outcome.Rounds.Should().Be(1);
        outcome.StandingVerdict.Should().BeNull();
        outcome.Final.Yaml.Should().Contain(BatchScope);
        rig.Published.Should().ContainSingle().Which.Current.Cause.Should()
            .Be($"spec review round 1 of {First}: build-with-cuts");
    }

    [Fact]
    public async Task ReviewLoop_ResidueLeft_RunsAnotherRound()
    {
        var rig = new ReviewLoopRig().Answers(Cuts(FlushCut), Build()).Revisions(GoalOnly);

        var outcome = await RunAsync(rig);

        outcome.Rounds.Should().Be(2);
        rig.Inputs.Should().HaveCount(2);
        rig.Inputs[1].PriorResidues.Should().ContainSingle()
            .Which.Should().Be(new SpecReviewResidue(SpecRegisterNames.Scope, FlushCut.Quote));
        outcome.StandingVerdict.Should().BeNull();
    }

    [Fact]
    public async Task ReviewLoop_RoundChangedNothing_EndsTheLoop()
    {
        var rig = new ReviewLoopRig().Answers(Cuts(FlushCut), Build());

        var outcome = await RunAsync(rig);

        outcome.Rounds.Should().Be(1, "a second reviewer over the same text is reviewer shopping");
        outcome.StandingVerdict.Should().Be(SpecReviewVerdicts.BuildWithCuts);
        rig.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task ReviewLoop_LastRound_PassesLastRoundToTheReviser()
    {
        var rig = new ReviewLoopRig().Answers(Cuts(FlushCut), Cuts(FlushCut))
            .Revisions(GoalOnly, ("into one dated file", "into one dated export file"));

        await RunAsync(rig, rounds: 2);

        rig.Reviser.Verify(r => r.ReviseAsync(It.IsAny<SpecDraft>(), It.IsAny<IReadOnlyList<SpecReviewCut>>(),
            It.IsAny<IReadOnlyList<SpecEvidenceProblem>>(), false, It.IsAny<AgentConfig>(),
            It.IsAny<PipelineCostTracker>(), It.IsAny<CancellationToken>()), Times.Once);
        rig.Reviser.Verify(r => r.ReviseAsync(It.IsAny<SpecDraft>(), It.IsAny<IReadOnlyList<SpecReviewCut>>(),
            It.IsAny<IReadOnlyList<SpecEvidenceProblem>>(), true, It.IsAny<AgentConfig>(),
            It.IsAny<PipelineCostTracker>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewLoop_RefuseWithoutCuts_RederivesOnceThenAFreshRound()
    {
        var rig = new ReviewLoopRig().Answers(Refuse(), Build());
        var rewrite = CutRevisionFixtures.Draft(CutRevisionFixtures.With(FlushScope, BatchScope));
        Rederives(rig, rewrite);

        var outcome = await RunAsync(rig);

        outcome.Rounds.Should().Be(2);
        outcome.StandingVerdict.Should().BeNull();
        outcome.Final.Yaml.Should().Contain(BatchScope);
        rig.Inputs[1].Draft.Yaml.Should().Contain(BatchScope, "the fresh round reviews the rewrite");
        rig.Published.Should().ContainSingle().Which.Current.Cause.Should().Contain("refuse, rewritten from the code");
        rig.Rederiver.Verify(r => r.RederiveAsync(It.IsAny<Contracts.Commands.PipelineContext>(), It.IsAny<SpecDraft>(),
            It.Is<IReadOnlyList<SpecReviewFinding>>(f => f.Count == 1), It.IsAny<string?>(), It.IsAny<AgentConfig>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewLoop_SecondRefuse_EndsStandingWithoutASecondRederive()
    {
        var rig = new ReviewLoopRig().Answers(Refuse(), Refuse(), Build());
        Rederives(rig, CutRevisionFixtures.Draft(CutRevisionFixtures.With(FlushScope, BatchScope)));

        var outcome = await RunAsync(rig);

        outcome.Rounds.Should().Be(2);
        outcome.StandingVerdict.Should().Be(SpecReviewVerdicts.Refuse);
        outcome.StandingFindings.Should().ContainSingle();
        rig.Rederiver.Verify(r => r.RederiveAsync(It.IsAny<Contracts.Commands.PipelineContext>(), It.IsAny<SpecDraft>(),
            It.IsAny<IReadOnlyList<SpecReviewFinding>>(), It.IsAny<string?>(), It.IsAny<AgentConfig>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewLoop_RoundsSpent_BuildsWithWhatAppliedAndNamesTheStandingVerdict()
    {
        var rig = new ReviewLoopRig().Answers(Cuts(FlushCut), Cuts(FlushCut))
            .Revisions(GoalOnly, ("into one dated file", "into one dated export file"));

        var outcome = await RunAsync(rig, rounds: 2);
        var line = (await new ReviewLoopRig().Answers(Cuts(FlushCut), Cuts(FlushCut))
            .Revisions(GoalOnly, ("into one dated file", "into one dated export file"))
            .ExecuteAsync(Settings(rounds: 2))).Message;

        outcome.Rounds.Should().Be(2);
        outcome.IsCleared.Should().BeTrue("exhausted rounds build the spec with what applied");
        outcome.StandingVerdict.Should().Be(SpecReviewVerdicts.BuildWithCuts);
        outcome.Final.Yaml.Should().Contain("into one dated export file");
        rig.Published.Should().HaveCount(2);
        line.Should().Contain($"{First}: build-with-cuts standing after 2 round(s), built with what applied");
    }

    [Fact]
    public async Task ReviewLoop_EachReviewer_PublishesOneEventAfterTheCutsWithResidues()
    {
        var rig = new ReviewLoopRig().Answers(Cuts(FlushCut), Cuts(FlushCut), Build(), Build()).Revisions(GoalOnly);

        await RunAsync(rig, reviewers: 2);

        rig.EventsAtPublish.First().Should().Be(0, "the round's revision is written before its answers are stored");
        var round = rig.Events.Events.Cast<SpecReviewRoundEvent>().Where(e => e.Round == 1).ToList();
        round.Select(e => e.Reviewer).Should().BeEquivalentTo([0, 1]);
        round.Should().OnlyContain(e => e.SpecId == First && e.RevisionNumber == 2 && e.Residues.Count == 1);
    }

    [Fact]
    public async Task ReviewPanel_EvidenceCheck_RunsOncePerTextVersion()
    {
        var rig = new ReviewLoopRig().Answers(Cuts(FlushCut), Build()).Revisions(GoalOnly);

        await RunAsync(rig);

        rig.Evidence.Verify(e => e.CheckAsync(It.IsAny<Contracts.Commands.PipelineContext>(), It.IsAny<SpecDraft>(),
            It.IsAny<SpecSet>(), It.IsAny<CancellationToken>()), Times.Exactly(2),
            "once on the derived text and once on the one revision; the BUILD round changed nothing");
    }

    private static void Rederives(ReviewLoopRig rig, SpecDraft rewrite) =>
        rig.Rederiver.Setup(r => r.RederiveAsync(It.IsAny<Contracts.Commands.PipelineContext>(), It.IsAny<SpecDraft>(),
                It.IsAny<IReadOnlyList<SpecReviewFinding>>(), It.IsAny<string?>(), It.IsAny<AgentConfig>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(rewrite);

    private static async Task<SpecReviewOutcome> RunAsync(ReviewLoopRig rig, int rounds = 4, int reviewers = 1)
    {
        using var _ = rig.RunContext.BeginScope(RunId);
        return await rig.Loop().RunAsync(rig.Run(rounds, reviewers), rig.Set.Specs[0], first: true, CancellationToken.None);
    }
}
