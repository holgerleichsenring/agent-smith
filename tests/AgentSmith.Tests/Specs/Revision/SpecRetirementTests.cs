using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Specs.Review;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Specs.Revision;

/// <summary>
/// 2026-11-27-3532d: the coding master retires the spec it builds and writes its replacement with revise_spec(retire). The
/// retired spec stays as written; the replacement gets the next free member id, stands right behind it and is what later
/// specs require; the replacement is the spec the run builds from then on.
/// </summary>
public sealed class SpecRetirementTests
{
    private const string Series = "2026-10-14-5678";
    private const string Minted = "2026-10-14-5678c";
    private const string Goal = "The export streams every order of the day into one file.";

    private static SpecRevisionRig Rig(IReadOnlyList<string>? executed = null)
    {
        var set = ReviewLoopRig.TwoSpecs(secondRequires: ReviewLoopRig.First) with { Series = Series, Executed = executed ?? [] };
        var rig = new SpecRevisionRig();
        rig.Pipeline.Set(ContextKeys.SpecSet, set);
        rig.Pipeline.Set(ContextKeys.CurrentSpec, set.Specs[0].Draft);
        return rig;
    }

    private static string Replacement(string requires = "2026-10-14-1234a") => CutRevisionFixtures.With(
        "requires: [2026-10-14-1234a]", $"requires: [{requires}]",
        CutRevisionFixtures.With("The export writes every order of the day into one file.", Goal));

    [Fact]
    public async Task Retire_TheCurrentSpec_MintsTheNextFreeIdAndRelinksLaterRequires()
    {
        var rig = Rig();

        var reply = await rig.RetireAsync(Replacement());

        reply.Should().Contain($"Spec {ReviewLoopRig.First} is retired").And.Contain($"superseded by {Minted}");
        var set = rig.Loop.Published.Should().ContainSingle().Subject;
        set.Specs.Select(s => s.SpecId).Should().Equal(ReviewLoopRig.First, Minted, ReviewLoopRig.Second);
        set.Retired().Should().Equal(ReviewLoopRig.First);
        set.UnexecutedTail.Select(s => s.SpecId).Should().Equal(Minted, ReviewLoopRig.Second);
        var replacement = set.Specs[1];
        replacement.Draft.Supersedes.Should().Equal(ReviewLoopRig.First);
        replacement.Draft.Yaml.Should().Contain($"spec: {Minted}").And.Contain($"supersedes: [{ReviewLoopRig.First}]");
        replacement.Slug.Should().Be(set.Specs[0].Slug, "the replacement keeps the retired spec's label");
        set.Specs[0].Draft.Yaml.Should().Be(CutRevisionFixtures.Draft().Yaml, "the retired spec stays as it was written");
        set.Specs[2].Draft.Requires.Should().Equal(Minted);
        set.Specs[2].Draft.Yaml.Should().Contain($"requires: [{Minted}]");
        set.Current.Cause.Should().StartWith($"retired in run {ReviewLoopRig.RunId}: {ReviewLoopRig.First} superseded by {Minted}");
        rig.Current.SpecId.Should().Be(Minted);
        rig.Current.Goal.Should().Be(Goal);
        var rows = rig.Pipeline.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress).Specs;
        rows.Select(r => (r.SpecId, r.State)).Should().Equal(
            (ReviewLoopRig.First, SpecRunState.Superseded), (Minted, SpecRunState.InProgress), (ReviewLoopRig.Second, SpecRunState.NotStarted));
        rig.Progress.Verify(p => p.RecordAsync(rig.Pipeline, ReviewLoopRig.First, SpecRunState.Superseded,
            null, It.IsAny<string?>(), false, It.IsAny<CancellationToken>()));
        rig.Logged.Should().ContainSingle().Which.Should().Contain($"Spec {ReviewLoopRig.First} retired");
    }

    [Fact]
    public async Task Retire_AReplacementRequiringTheRetiredSpec_IsRefused()
    {
        var rig = Rig();

        var reply = await rig.RetireAsync(Replacement(requires: ReviewLoopRig.First));

        reply.Should().StartWith("Not revised").And.Contain("cannot require it");
        rig.Loop.Published.Should().BeEmpty();
        rig.Current.SpecId.Should().Be(ReviewLoopRig.First);
    }

    [Fact]
    public async Task Retire_AReplacementRequiringALaterSpec_IsRefused()
    {
        var rig = Rig();

        var reply = await rig.RetireAsync(Replacement(requires: ReviewLoopRig.Second));

        reply.Should().StartWith("Not revised").And.Contain("comes after it in the series");
        rig.Loop.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task Retire_AnExecutedSpec_IsRefused()
    {
        var rig = Rig(executed: [ReviewLoopRig.First]);

        var reply = await rig.RetireAsync(Replacement());

        reply.Should().StartWith("Not revised").And.Contain("already ran, so it is never retired");
        rig.Loop.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task Retire_TheBranchRefusedTheWrite_PutsTheSetBack()
    {
        var rig = Rig();
        var before = rig.Pipeline.Get<SpecSet>(ContextKeys.SpecSet);
        rig.Loop.WritesToBranch = false;

        var reply = await rig.RetireAsync(Replacement());

        reply.Should().StartWith("Not revised");
        rig.Pipeline.Get<SpecSet>(ContextKeys.SpecSet).Should().BeSameAs(before);
        rig.Current.SpecId.Should().Be(ReviewLoopRig.First);
    }

    [Fact]
    public async Task Retire_PastTheLastMemberLetter_IsRefusedNamingTheLetters()
    {
        var rig = Rig();
        rig.Pipeline.Set<IReadOnlyList<string>>(ContextKeys.SpecTakenIds,
            [.. Enumerable.Range(0, 26).Select(i => $"{Series}{(char)('a' + i)}")]);

        var reply = await rig.RetireAsync(Replacement());

        reply.Should().StartWith("Not revised").And.Contain("26 member letters (a..z)");
        rig.Loop.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task Retire_TheRetiredSpecsAccounts_AreDropped()
    {
        var rig = Rig();
        rig.Pipeline.Set(ContextKeys.RunAccounts, RunAccounts.Empty
            .With(ReviewLoopRig.First, [new SpecAccount("shop", [], "red")]));

        await rig.RetireAsync(Replacement());

        rig.Pipeline.Get<RunAccounts>(ContextKeys.RunAccounts).Specs.Should().BeEmpty(
            "a retired spec answers for nothing, so its draft-keeping account goes");
    }
}
