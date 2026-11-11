using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using FluentAssertions;
using static AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>
/// 2026-11-11-2bdfb: an epic child refused without cuts may be re-cut — split, merged, re-homed — and the next round
/// reviews the new and changed specs; a revision not used says why.
/// </summary>
public sealed class ProposalReviewRecutTests
{
    private const string Third = "2026-10-14-9000d";
    private const string Step = "Write the orders through the buffered file writer.";
    private const string NewStep = "Write the orders through the streaming file writer.";
    private static readonly string ParentYaml = Yaml(Parent, "The export is rebuilt in two slices.");

    private static string Children(params string[] children) => EpicReply(ParentYaml, children);

    // An epic keeps at least two children, so a test that drops or omits one starts from three.
    private static string Three() => Children(Yaml(First), Yaml(Second, requires: [First]), Yaml(Third, "The archive half."));

    private static SpecAttackResult SecondRefused(SpecAttackInput input) =>
        input.Round == 1 && input.Draft.SpecId == Second ? Refuse("the slice holds two capabilities") : Build();

    [Fact]
    public async Task ProposalReview_RefusedChildSplit_SecondRoundReviewsBothAndPublishesThem()
    {
        var checkedIds = new List<string>();
        var rig = new ProposalReviewRig { OnReview = SecondRefused };
        rig.OnEvidence = draft => { lock (checkedIds) checkedIds.Add(draft.SpecId); return SpecRoundEvidenceResult.None; };
        rig.Propose(Epic());
        rig.Replies(_ => Children(Yaml(First), Yaml(Second, "The import half.", requires: [First]),
            Yaml(Third, "The archive half.", requires: [Second])));

        await rig.RunAsync();

        ((EpicOutcome)rig.Outcome).Children.Select(c => c.SpecId).Should().Equal(First, Second, Third);
        rig.Reviewed.Where(i => i.Round == 2).Select(i => i.Draft.SpecId).Distinct().Should().BeEquivalentTo([Second, Third]);
        rig.Outcome.ReviewRounds.Should().Contain(r => r.SpecId == Third && r.Round == 2);
        rig.Outcome.NotReviewed.Should().BeNull();
        checkedIds.Should().Contain(Third, "an added child's evidence is checked");
    }

    [Fact]
    public async Task ProposalReview_RefusedChildRehomed_ReposChangeAdmitted()
    {
        var rig = new ProposalReviewRig { OnReview = SecondRefused };
        rig.Propose(Epic());
        var rehomed = Yaml(Second, "The import reads the file.", requires: [First])
            .Replace("applies_to: \"backend (export)\"", "applies_to: \"erp (import)\"");
        rig.Replies(_ => Children(Yaml(First), rehomed));

        await rig.RunAsync();

        rig.DraftOf(Second).Yaml.Should().Contain("erp (import)", "a child refused without cuts may change its identity");
        rig.DraftOf(Second).Goal.Should().Be("The import reads the file.");
    }

    [Fact]
    public async Task ProposalReview_AddedIdWithoutRefusal_LeftOutAndListedReverted()
    {
        const string Later = "Write the orders through the batched file writer.";
        var rig = new ProposalReviewRig { Settings = Of(rounds: 2) };
        rig.OnReview = input => input.Draft.SpecId != First ? Build()
            : RefuseCuts(input.Round == 1 ? Cut(SpecRegisterNames.Steps, Step, NewStep) : Cut(SpecRegisterNames.Steps, NewStep, Later));
        rig.Propose(Epic());
        rig.Replies(_ => Children(Yaml(First, step: NewStep), Yaml(Second, requires: [First]), Yaml(Third, "An unasked slice.")),
            _ => Children(Yaml(First, step: Later), Yaml(Second, requires: [First])));

        await rig.RunAsync();

        ((EpicOutcome)rig.Outcome).Children.Select(c => c.SpecId).Should().Equal(First, Second);
        rig.Nudges[1].Should().Contain($"## {Third} was left out").And.Contain(ProposalCutsNudge.LeftOutLine);
        rig.DraftOf(First).Yaml.Should().Contain(Later);
    }

    [Fact]
    public async Task ProposalReview_UnlicensedChildMissing_RestoredInLastValidText()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 2) };
        rig.OnReview = input => input.Draft.SpecId == Second ? Refuse("the slice holds two capabilities")
            : input.Round == 1 ? RefuseCuts(Cut(SpecRegisterNames.Steps, Step, NewStep)) : Build();
        var first = rig.Propose(Three());
        rig.Replies(_ => Children(Yaml(Second, "The import reads the file."), Yaml(Third, "The archive half.")), _ => Three());

        await rig.RunAsync();

        rig.DraftOf(First).Yaml.Should().Be(((EpicOutcome)first).Children[0].Yaml, "a missing unlicensed child is restored");
        rig.Nudges[1].Should().Contain($"## {First} was reverted");
    }

    [Fact]
    public async Task ProposalReview_UnlicensedChildIdentityChanged_RevertedAlone()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 1) };
        rig.OnReview = input => input.Draft.SpecId == Second ? Refuse("the slice holds two capabilities")
            : Cuts(Cut(SpecRegisterNames.Steps, Step, NewStep));
        var first = rig.Propose(Epic());
        rig.Replies(_ => Children(
            Yaml(First, step: NewStep).Replace("applies_to: \"backend (export)\"", "applies_to: \"erp (import)\""),
            Yaml(Second, "The import reads the file.", requires: [First])));

        await rig.RunAsync();

        rig.DraftOf(First).Yaml.Should().Be(((EpicOutcome)first).Children[0].Yaml, "a cut licenses no identity change");
        rig.DraftOf(Second).Goal.Should().Be("The import reads the file.", "the refused sibling's rewrite stands");
    }

    [Fact]
    public async Task ProposalReview_DroppedIdStillRequired_ReplyVoidWithReasonOnResult()
    {
        var rig = new ProposalReviewRig();
        rig.OnReview = input => input.Draft.SpecId == First ? Refuse("the writer does not exist")
            : Cuts(Cut(SpecRegisterNames.Steps, Step, NewStep));
        var first = rig.Propose(Three());
        rig.Replies(_ => Children(Yaml(Second, step: NewStep), Yaml(Third, "The archive half.")));

        await rig.RunAsync();

        ((EpicOutcome)rig.Outcome).Children.Should().Equal(((EpicOutcome)first).Children);
        var why = ProposalReviewSummary.NotUsedWhy(ProposalReProposalGuard.StillRequired(First, Second));
        rig.Outcome.Findings.Where(f => f.Problem == ProposalReviewSummary.NotUsedProblem)
            .Should().HaveCount(3, "every draft that went into the re-prompt").And.OnlyContain(f => f.Why == why);
        rig.Nudges.Should().ContainSingle("a void reply ends the review");
    }

    [Fact]
    public async Task ProposalReview_MergedSetCycle_ReplyVoid()
    {
        var rig = new ProposalReviewRig();
        rig.OnReview = input => input.Draft.SpecId == First ? Refuse("the writer does not exist")
            : Cuts(Cut(SpecRegisterNames.Steps, Step, NewStep));
        rig.Propose(Epic());
        rig.Replies(_ => Children(Yaml(First, "The export waits on the import.", requires: [Second]), Yaml(Second, step: NewStep)));

        await rig.RunAsync();

        rig.DraftOf(First).Requires.Should().BeEmpty("the void reply leaves the last valid proposal");
        rig.Outcome.Findings.Should().Contain(f => f.Problem == ProposalReviewSummary.NotUsedProblem
            && f.SpecId == First && f.Why!.Contains("requires do not hold"));
    }

    [Fact]
    public async Task ProposalReview_KindChanged_ReplyVoidWithReasonOnResult()
    {
        var rig = new ProposalReviewRig { OnReview = SecondRefused };
        var epic = rig.Propose(Epic());
        rig.Replies(_ => SpecReply(Yaml(Lone)));

        await rig.RunAsync();

        rig.Outcome.Should().BeOfType<EpicOutcome>();
        rig.Outcome.Findings.Should().ContainSingle(f => f.Problem == ProposalReviewSummary.NotUsedProblem)
            .Which.Why.Should().Be(ProposalReviewSummary.NotUsedWhy(ProposalReProposalGuard.OtherKind));
        var result = new ProposalReProposalGuard(new SpecRevisionGuard()).Merge(epic,
            new SpecOutcome(((EpicOutcome)epic).Children[0]), new Dictionary<string, ProposalDraftLicence>(), false, false);
        result.Void.Should().Be(ProposalReProposalGuard.OtherKind);
        result.Merged.Should().BeSameAs(epic);
    }

    [Fact]
    public async Task ProposalReview_RecutOnLastRound_NewChildPublishedNotReviewed()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 1), OnReview = SecondRefused };
        rig.Propose(Epic());
        rig.Replies(_ => Children(Yaml(First), Yaml(Second, "The import half.", requires: [First]), Yaml(Third, "The archive half.")));

        await rig.RunAsync();

        ((EpicOutcome)rig.Outcome).Children.Select(c => c.SpecId).Should().Contain(Third);
        rig.Outcome.NotReviewed.Should().Contain(Third);
        rig.Outcome.Findings.Should().Contain(f => f.SpecId == Third
            && f.Why == ProposalReviewSummary.NotReviewedWhy(ProposalReviewSummary.LastRevisionWhy));
        rig.Outcome.ReviewRounds.Should().NotContain(r => r.SpecId == Third);
    }

    [Fact]
    public async Task ProposalReview_DroppedChild_NoRoundsOrFindingsPublished()
    {
        var rig = new ProposalReviewRig
        {
            OnReview = SecondRefused,
            WholeSet = new([new CutFinding(Second, "", "ordering", "the second slice repeats the first")]),
        };
        rig.Propose(Three());
        rig.Replies(_ => Children(Yaml(First), Yaml(Third, "The archive half.")));

        await rig.RunAsync();

        ((EpicOutcome)rig.Outcome).Children.Select(c => c.SpecId).Should().Equal(First, Third);
        rig.Outcome.ReviewRounds.Should().NotContain(r => r.SpecId == Second);
        rig.Outcome.Findings.Should().NotContain(f => f.SpecId == Second, "the whole-set finding on the dropped id goes too");
        rig.Outcome.NotReviewed.Should().BeNull();
    }

    [Fact]
    public async Task ProposalReview_RecutWithContract_ReplyTextCarriesThePublishedContract()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 1), OnReview = SecondRefused };
        rig.Propose(ContractEpic());
        rig.Replies(_ => EpicReply(ParentYaml, [Yaml(First), Yaml(Second, requires: [First]), Yaml(Third, "The archive half.")],
            contract: new Dictionary<object, object?>
            {
                ["names"] = new List<object?>
                {
                    new Dictionary<object, object?> { ["name"] = "OrderArchived", ["kind"] = "event", ["number"] = 88, ["owner"] = Third },
                },
            }));

        var reply = await rig.RunAsync();

        ((EpicOutcome)rig.Outcome).Contract!.Names.Should().ContainSingle().Which.Owner.Should().Be(Third);
        reply.Response.Text.Should().StartWith("Here is the epic.").And.Contain("OrderArchived").And.NotContain("OrderExported");
    }

    [Fact]
    public async Task ProposalCutsNudge_RefusedChild_OffersRecut()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 1), OnReview = SecondRefused };
        rig.Propose(Epic());
        rig.Replies(_ => Epic());

        await rig.RunAsync();

        rig.Nudges.Single().Should().Contain(ProposalCutsNudge.RecutFromFindings).And.NotContain(ProposalCutsNudge.RewriteFromFindings);
    }

    [Fact]
    public async Task ProposalCutsNudge_RefusedLoneSpec_RewriteOnly()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 1), OnReview = _ => Refuse("the writer does not exist") };
        rig.Propose(SpecReply(Yaml(Lone)));
        rig.Replies(_ => SpecReply(Yaml(Lone)));

        await rig.RunAsync();

        rig.Nudges.Single().Should().Contain(ProposalCutsNudge.RewriteFromFindings).And.NotContain(ProposalCutsNudge.RecutFromFindings);
    }
}
