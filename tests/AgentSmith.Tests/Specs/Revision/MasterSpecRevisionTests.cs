using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.Specs.Review;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.Revision;

/// <summary>
/// 2026-11-17-ec48a: the coding master revises its current spec — a changed text is one revision on the branch under the
/// run's cause and becomes the spec's criteria; anything else writes nothing and says why. 2026-11-19-3e10: no reviewer
/// judges the text; only the mechanical checks stand between it and the branch.
/// </summary>
public sealed class MasterSpecRevisionTests
{
    [Fact]
    public async Task Revise_AValidChangedText_IsWrittenAsTheNextRevisionWithoutAReview()
    {
        var rig = new SpecRevisionRig();
        rig.Pipeline.Set(ContextKeys.SpecAccounts, new List<AgentSmith.Domain.Models.SpecAccount>());

        var reply = await rig.ReviseAsync(SpecRevisionRig.RevisedYaml);

        reply.Should().Contain("revision 2").And.Contain(SpecRevisionRig.Revised).And.Contain("update_progress")
            .And.NotContain("review");
        rig.Loop.Published.Should().ContainSingle();
        rig.Loop.Set.Current.Cause.Should().Be($"revised in run {ReviewLoopRig.RunId}: the operator asked for a retry");
        rig.Current.Done.Should().Contain(SpecRevisionRig.Revised).And.NotContain(SpecRevisionRig.Criterion);
        rig.Current.Repos.Should().Equal("shop");
        rig.Pipeline.Has(ContextKeys.SpecAccounts).Should().BeFalse("the entry account was taken on the old criteria");
        rig.Logged.Should().ContainSingle().Which.Should().Contain("revision 2").And.NotContain("review");
        rig.Loop.Events.Events.OfType<SpecReviewRoundEvent>().Should().BeEmpty("no review round is held or stored");
    }

    [Theory]
    [InlineData("goal: [unclosed")]
    [InlineData("spec: 2026-10-14-5678a\n")]
    public async Task Revise_AnInvalidText_WritesNothing(string yaml)
    {
        var rig = new SpecRevisionRig();

        var reply = await rig.ReviseAsync(yaml);

        reply.Should().StartWith("Not revised").And.Contain("invalid");
        rig.Loop.Published.Should().BeEmpty();
        rig.Current.Done.Should().Contain(SpecRevisionRig.Criterion);
    }

    [Fact]
    public async Task Revise_AnIdentityMove_WritesNothing()
    {
        var rig = new SpecRevisionRig();

        var reply = await rig.ReviseAsync(CutRevisionFixtures.With("spec: 2026-10-14-5678a", "spec: 2026-10-14-9999a",
            SpecRevisionRig.RevisedYaml));

        reply.Should().StartWith("Not revised").And.Contain("keeps spec");
        rig.Loop.Published.Should().BeEmpty();
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
    public async Task Revise_Twice_WritesTwoRevisions()
    {
        var rig = new SpecRevisionRig();

        await rig.ReviseAsync(SpecRevisionRig.RevisedYaml);
        await rig.ReviseAsync(CutRevisionFixtures.With(SpecRevisionRig.Criterion, "A failed write is retried twice."));

        rig.Loop.Set.Current.Number.Should().Be(3);
    }

    [Fact]
    public async Task Revise_NoReason_IsRefusedBeforeAnything()
    {
        var rig = new SpecRevisionRig();

        (await rig.ReviseAsync(SpecRevisionRig.RevisedYaml, " ")).Should().Contain("give the reason");
        rig.Loop.Published.Should().BeEmpty();
    }

    // 2026-11-19-6b0ba: the spec its master wrote is no longer the ticket's stand-in.
    [Fact]
    public async Task Revise_AStandIn_TakesItOffTheStandIns()
    {
        var rig = new SpecRevisionRig();
        var set = rig.Pipeline.Get<SpecSet>(ContextKeys.SpecSet);
        rig.Pipeline.Set(ContextKeys.SpecSet, set with { StandIns = [rig.Current.SpecId] });

        await rig.ReviseAsync(SpecRevisionRig.RevisedYaml);

        rig.Loop.Set.StandIns.Should().BeEmpty();
    }
}
