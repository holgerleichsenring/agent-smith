using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.Specs.Review;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Specs.Revision;

/// <summary>
/// 2026-11-17-ec48a: the coding master revises its current spec — one review decides; a cleared text is one revision
/// on the branch under the run's cause and becomes the spec's criteria; anything else writes nothing and says why.
/// </summary>
public sealed class MasterSpecRevisionTests
{
    [Fact]
    public async Task Revise_ReviewSaysBuild_WritesOneRevisionAndTheCriteriaBecomeTheRevisedOnes()
    {
        var rig = new SpecRevisionRig();
        rig.Pipeline.Set(ContextKeys.SpecAccounts, new List<AgentSmith.Domain.Models.SpecAccount>());

        var reply = await rig.ReviseAsync(SpecRevisionRig.RevisedYaml);

        reply.Should().Contain("revision 2").And.Contain(SpecRevisionRig.Revised).And.Contain("update_progress");
        rig.Loop.Published.Should().ContainSingle();
        rig.Loop.Set.Current.Cause.Should().Be($"revised in run {ReviewLoopRig.RunId}: the operator asked for a retry");
        rig.Current.Done.Should().Contain(SpecRevisionRig.Revised).And.NotContain(SpecRevisionRig.Criterion);
        rig.Current.Repos.Should().Equal("shop");
        rig.Pipeline.Has(ContextKeys.SpecAccounts).Should().BeFalse("the entry account was taken on the old criteria");
        rig.Logged.Should().ContainSingle().Which.Should().Contain("revision 2").And.Contain("build");
        var round = rig.Loop.Events.Events.OfType<SpecReviewRoundEvent>().Should().ContainSingle().Subject;
        round.Round.Should().Be(5, "a revision review is a round after the four pre-build rounds");
        round.RevisionNumber.Should().Be(2);
    }

    [Fact]
    public async Task Revise_ReviewCuts_WritesTheCutText()
    {
        var rig = new SpecRevisionRig();
        rig.Loop.Answers(ReviewLoopRig.Cuts(CutRevisionFixtures.Cut("done", "retried once", "retried twice")))
            .Revisions(("retried once", "retried twice"));

        var reply = await rig.ReviseAsync(SpecRevisionRig.RevisedYaml);

        reply.Should().Contain("1 cut(s) applied");
        rig.Current.Done.Should().Contain("A failed write is logged with the order id and retried twice.");
        rig.Loop.Set.Specs[0].Draft.Yaml.Should().Contain("retried twice");
    }

    [Fact]
    public async Task Revise_ReviewRefuses_WritesNothingAndReturnsTheFindings()
    {
        var rig = new SpecRevisionRig();
        rig.Loop.Answers(ReviewLoopRig.Refuse("Nothing retries a failed write today."));

        var reply = await rig.ReviseAsync(SpecRevisionRig.RevisedYaml);

        reply.Should().StartWith("Not revised").And.Contain("Nothing retries a failed write today.").And.Contain("ask_human");
        rig.Loop.Published.Should().BeEmpty();
        rig.Current.Done.Should().Contain(SpecRevisionRig.Criterion);
        rig.Loop.Events.Events.OfType<SpecReviewRoundEvent>().Should().ContainSingle()
            .Which.RevisionNumber.Should().BeNull();
    }

    [Fact]
    public async Task Revise_UnresolvedCitation_AsksNoReviewer()
    {
        var rig = new SpecRevisionRig();
        rig.Loop.Evidence.Setup(e => e.CheckAsync(It.IsAny<AgentSmith.Contracts.Commands.PipelineContext>(),
                It.IsAny<AgentSmith.Contracts.Models.SpecDraft>(), It.IsAny<SpecSet>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentSmith.Application.Services.Specs.Review.SpecRoundEvidenceResult([CutRevisionFixtures.Problem()], 0, 2));

        var reply = await rig.ReviseAsync(SpecRevisionRig.RevisedYaml);

        reply.Should().Contain("do not resolve").And.Contain("OrderStore.cs has 31 lines");
        rig.Loop.Inputs.Should().BeEmpty();
        rig.Loop.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task Revise_ReviewOff_WritesUnreviewed()
    {
        var rig = new SpecRevisionRig { Settings = ReviewLoopRig.Settings(rounds: 0) };

        var reply = await rig.ReviseAsync(SpecRevisionRig.RevisedYaml);

        reply.Should().Contain("unreviewed");
        rig.Loop.Inputs.Should().BeEmpty();
        rig.Loop.Published.Should().ContainSingle();
        rig.Logged.Should().ContainSingle().Which.Should().Contain("spec_review.rounds is 0");
    }

    [Fact]
    public async Task Revise_BranchDidNotTakeTheWrite_ChangesNothing()
    {
        var rig = new SpecRevisionRig();
        rig.Loop.WritesToBranch = false;
        var before = rig.Loop.Set;

        var reply = await rig.ReviseAsync(SpecRevisionRig.RevisedYaml);

        reply.Should().StartWith("Not revised").And.Contain("was not written");
        rig.Loop.Set.Should().BeSameAs(before);
        rig.Current.Done.Should().Contain(SpecRevisionRig.Criterion);
        rig.Logged.Should().BeEmpty();
    }

    [Fact]
    public async Task Revise_Twice_EachReviewIsARoundOfItsOwn()
    {
        var rig = new SpecRevisionRig();

        await rig.ReviseAsync(SpecRevisionRig.RevisedYaml);
        await rig.ReviseAsync(CutRevisionFixtures.With(SpecRevisionRig.Criterion, "A failed write is retried twice."));

        rig.Loop.Events.Events.OfType<SpecReviewRoundEvent>().Select(e => e.Round).Should().Equal(5, 6);
        rig.Loop.Set.Current.Number.Should().Be(3);
    }

    [Fact]
    public async Task Revise_TheReviewerReadsTheReasonAndTheOperatorsMessages()
    {
        var rig = new SpecRevisionRig();
        rig.Pipeline.Set<IReadOnlyList<string>>(ContextKeys.TicketMessagesHanded, ["retry a failed write once"]);

        await rig.ReviseAsync(SpecRevisionRig.RevisedYaml, "the operator asked for one retry");

        var input = rig.Loop.Inputs.Should().ContainSingle().Subject;
        input.WiderGoal.Should().Contain("because: the operator asked for one retry");
        input.OperatorTurns.Should().Equal("retry a failed write once");
    }

    [Fact]
    public async Task Revise_NoReason_IsRefusedBeforeAnything()
    {
        var rig = new SpecRevisionRig();

        (await rig.ReviseAsync(SpecRevisionRig.RevisedYaml, " ")).Should().Contain("give the reason");
        rig.Loop.Inputs.Should().BeEmpty();
    }
}
