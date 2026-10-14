using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using FluentAssertions;
using static AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>
/// 2026-10-13-8762i: a design turn's proposal goes through review rounds before a person can approve it — the
/// design partner applies the cuts, and each round stays on the proposal.
/// </summary>
public sealed class ProposalReviewRoundsTests
{
    private const string Step = "Write the orders through the buffered file writer.";
    private const string NewStep = "Write the orders through the streaming file writer.";

    [Fact]
    public async Task ProposalReview_RoundsZero_IsTodaysSingleReview()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 0) };
        rig.Propose(SpecReply(Yaml(Lone)));

        var reply = await rig.RunAsync();

        rig.Reviewed.Should().BeEmpty("rounds 0 makes no attack call");
        rig.WholeSetCalls.Should().Be(1);
        rig.Outcome.ReviewRounds.Should().BeEmpty();
        rig.Outcome.NotReviewed.Should().BeNull();
        reply.Should().BeSameAs(rig.FirstReply);
    }

    [Fact]
    public async Task ProposalReview_RoundsOn_StillRunsTheWholeSetCutReviewOnce()
    {
        var rig = new ProposalReviewRig
        {
            WholeSet = new(
                [new AgentSmith.Domain.Models.CutFinding(Second, "", "ordering", "the second slice needs the first")]),
        };
        rig.Propose(Epic());
        rig.OnReview = input => input.Round == 1 ? RefuseCuts(Cut(SpecRegisterNames.Steps, Step, NewStep)) : Build();
        rig.Replies(_ => Epic(Yaml(First, step: NewStep), Yaml(Second, step: NewStep, requires: [First])));

        await rig.RunAsync();

        rig.WholeSetCalls.Should().Be(1, "the whole-set call runs once, before round 1");
        rig.Outcome.Findings.Should().Contain(f => f.SpecId == Second && f.Problem == "ordering");
        rig.Outcome.ReviewRounds.Should().HaveCount(4, "each child has two rounds");
    }

    [Fact]
    public async Task ProposalReview_FirstRoundBuild_NoRePrompt_OneRoundRecorded()
    {
        var rig = new ProposalReviewRig();
        rig.Propose(SpecReply(Yaml(Lone)));

        var reply = await rig.RunAsync();

        rig.Nudges.Should().BeEmpty();
        var round = rig.Outcome.ReviewRounds.Should().ContainSingle().Subject;
        round.Should().BeEquivalentTo(new { SpecId = Lone, Round = 1, Verdict = SpecReviewVerdicts.Build });
        reply.Should().BeSameAs(rig.FirstReply);
    }

    [Fact]
    public async Task ProposalReview_Cuts_RePromptsThePartnerOnceWithTheCutsAndTheThread()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 1) };
        rig.Propose(SpecReply(Yaml(Lone)));
        rig.OnReview = _ => Cuts(Cut(SpecRegisterNames.Steps, Step, NewStep));
        rig.Replies(_ => SpecReply(Yaml(Lone, step: NewStep)));

        await rig.RunAsync();

        var nudge = rig.Nudges.Should().ContainSingle().Subject;
        nudge.Should().Contain("register: steps").And.Contain($"quote: \"{Step}\"").And.Contain($"\"{NewStep}\"")
            .And.Contain(ProposalCutsNudge.ApplyExactly).And.Contain(ProposalCutsNudge.ReProposeWhole);
        rig.ThreadLengths.Single().Should().BeGreaterThan(0, "the re-prompt continues the turn's thread");
    }

    [Fact]
    public async Task ProposalReview_RevisedProposal_IsReviewedAgain_UntilBuild()
    {
        var rig = new ProposalReviewRig();
        rig.Propose(SpecReply(Yaml(Lone)));
        rig.OnReview = input => input.Draft.Yaml.Contains(NewStep) ? Build() : RefuseCuts(Cut(SpecRegisterNames.Steps, Step, NewStep));
        rig.Replies(_ => SpecReply(Yaml(Lone, step: NewStep)));

        await rig.RunAsync();

        rig.Outcome.ReviewRounds.Select(r => r.Verdict).Should().Equal(SpecReviewVerdicts.Refuse, SpecReviewVerdicts.Build);
        rig.DraftOf(Lone).Yaml.Should().Contain(NewStep);
        rig.Outcome.NotReviewed.Should().BeNull();
    }

    [Fact]
    public async Task ProposalReview_InvalidReProposal_KeepsTheLastValidProposal()
    {
        var rig = new ProposalReviewRig();
        var proposal = rig.Propose(SpecReply(Yaml(Lone)));
        rig.OnReview = _ => Cuts(Cut(SpecRegisterNames.Steps, Step, NewStep));
        rig.Replies(_ => "```yaml\nspec: [unclosed\n```");

        var reply = await rig.RunAsync();

        rig.DraftOf(Lone).Yaml.Should().Be(((SpecOutcome)proposal).Draft.Yaml);
        reply.Should().BeSameAs(rig.FirstReply);
        rig.Outcome.ReviewRounds.Should().ContainSingle().Which.Residues.Should().NotBeEmpty("the cut still stands");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProposalReview_ReProposalChangedIdsOrKind_KeepsLastValid(bool kind)
    {
        var rig = new ProposalReviewRig();
        var proposal = rig.Propose(SpecReply(Yaml(Lone)));
        rig.OnReview = _ => Cuts(Cut(SpecRegisterNames.Steps, Step, NewStep));
        rig.Replies(_ => kind ? Epic() : SpecReply(Yaml("2026-10-14-7000b", step: NewStep)));

        await rig.RunAsync();

        rig.Outcome.Should().BeOfType<SpecOutcome>().Which.Draft.Yaml.Should().Be(((SpecOutcome)proposal).Draft.Yaml);
        rig.Nudges.Should().ContainSingle("an invalid re-proposal ends the review");
    }

    [Fact]
    public async Task ProposalReview_ChildOrderChanged_KeepsLastValid()
    {
        var rig = new ProposalReviewRig();
        rig.Propose(Epic());
        rig.OnReview = input => input.Draft.SpecId == First ? Cuts(Cut(SpecRegisterNames.Steps, Step, NewStep)) : Build();
        rig.Replies(_ => EpicReply(Yaml(Parent, "The export is rebuilt in two slices."),
            [Yaml(Second), Yaml(First, step: NewStep)]));

        await rig.RunAsync();

        ((EpicOutcome)rig.Outcome).Children.Select(c => c.SpecId).Should().Equal(First, Second);
        rig.DraftOf(First).Yaml.Should().Contain(Step);
    }

    [Fact]
    public async Task ProposalReview_ReProposalChangedUncutText_RevertsThatDraftOnly()
    {
        var rig = new ProposalReviewRig();
        rig.Propose(Epic());
        rig.OnReview = input => input.Draft.SpecId == First && input.Round == 1
            ? Cuts(Cut(SpecRegisterNames.Steps, Step, NewStep)) : Build();
        rig.Replies(_ => Epic(Yaml(First, step: NewStep), Yaml(Second, "A goal nobody cut.", requires: [First])));

        await rig.RunAsync();

        rig.DraftOf(First).Yaml.Should().Contain(NewStep, "its cut was applied");
        rig.DraftOf(Second).Goal.Should().NotBe("A goal nobody cut.", "nothing licensed its change");
    }

    [Fact]
    public async Task ProposalReview_OneDraftRefusedByTheGuard_OthersKeepTheirCuts()
    {
        var rig = new ProposalReviewRig();
        rig.Propose(Epic());
        rig.OnReview = input => input.Round == 1 ? Cuts(Cut(SpecRegisterNames.Steps, Step, NewStep)) : Build();
        rig.Replies(_ => Epic(Yaml(First, step: NewStep),
            Yaml(Second, "A rewritten goal beyond the cut.", step: NewStep, requires: [First])));

        await rig.RunAsync();

        rig.DraftOf(First).Yaml.Should().Contain(NewStep);
        rig.DraftOf(Second).Yaml.Should().Contain(Step).And.NotContain("A rewritten goal");
    }

    [Fact]
    public async Task ProposalReview_RefuseWithoutCuts_PartnerRewrite_IsAdmittedUnderTheIdentityRule()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 2) };
        rig.Propose(SpecReply(Yaml(Lone)));
        rig.OnReview = input => input.Round == 1 ? Refuse("the writer does not exist") : Build();
        rig.Replies(_ => SpecReply(Yaml(Lone, "The export streams every order into one file.", NewStep)));

        await rig.RunAsync();

        rig.Nudges.Single().Should().Contain(ProposalCutsNudge.RewriteFromFindings).And.Contain("the writer does not exist");
        rig.DraftOf(Lone).Goal.Should().Be("The export streams every order into one file.");
        rig.Outcome.ReviewRounds.Select(r => r.Verdict).Should().Equal(SpecReviewVerdicts.Refuse, SpecReviewVerdicts.Build);
    }

    [Fact]
    public async Task ProposalReview_LastRoundCuts_AreAppliedWithoutAFurtherReview()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 1) };
        rig.Propose(SpecReply(Yaml(Lone)));
        rig.OnReview = _ => Cuts(Cut(SpecRegisterNames.Steps, Step, NewStep));
        rig.Replies(_ => SpecReply(Yaml(Lone, step: NewStep)));

        await rig.RunAsync();

        rig.DraftOf(Lone).Yaml.Should().Contain(NewStep);
        rig.Reviewed.Should().HaveCount(2, "one round of two reviewers, and none after the last re-prompt");
        rig.Outcome.ReviewRounds.Single().Residues.Should().BeEmpty();
    }

    [Fact]
    public async Task ProposalReview_ChildRequiringAnUnbuiltSibling_DoesNotDescribeToday()
    {
        var rig = new ProposalReviewRig();
        rig.Propose(Epic());

        await rig.RunAsync();

        rig.Reviewed.Where(i => i.Draft.SpecId == First).Should().OnlyContain(i => i.DescribesToday);
        rig.Reviewed.Where(i => i.Draft.SpecId == Second).Should().OnlyContain(i => !i.DescribesToday,
            "its requires names a sibling no run has built");
    }

    [Fact]
    public async Task ProposalReview_EpicParent_GetsNoAttackRounds()
    {
        var checkedDrafts = new List<string>();
        var rig = new ProposalReviewRig();
        rig.OnEvidence = draft =>
        {
            lock (checkedDrafts) checkedDrafts.Add(draft.SpecId);
            return draft.SpecId == Parent
                ? new([new SpecEvidenceProblem("the export exists", "src/Export.cs is absent")], 0, 1)
                : SpecRoundEvidenceResult.None;
        };
        rig.Propose(Epic());

        await rig.RunAsync();

        rig.Reviewed.Should().NotContain(i => i.Draft.SpecId == Parent);
        rig.Outcome.ReviewRounds.Should().NotContain(r => r.SpecId == Parent);
        checkedDrafts.Count(id => id == Parent).Should().Be(1, "the parent gets one evidence check");
        rig.Outcome.Findings.Should().Contain(f => f.SpecId == Parent && f.Quote == "the export exists");
    }

    [Fact]
    public async Task ProposalReview_DraftReverted_ReplyTextCarriesThePublishedDraft()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 1) };
        rig.Propose(Epic());
        rig.OnReview = input => input.Draft.SpecId == First ? Cuts(Cut(SpecRegisterNames.Steps, Step, NewStep)) : Build();
        rig.Replies(_ => Epic(Yaml(First, step: NewStep), Yaml(Second, "An unlicensed goal.", requires: [First]),
            parent: null) + "\nI applied the cut.");

        var reply = await rig.RunAsync();

        reply.Response.Text.Should().StartWith("Here is the epic.", "the first reply's prose is kept")
            .And.Contain(NewStep).And.NotContain("An unlicensed goal.");
        reply.Response.Text.Should().NotContain("I applied the cut.");
    }

    [Fact]
    public async Task ProposalReview_LoopEndsOnEarlierProposal_ReplyIsThatProposal()
    {
        const string Third = "Write the orders through the batched file writer.";
        var rig = new ProposalReviewRig();
        rig.Propose(SpecReply(Yaml(Lone)));
        rig.OnReview = input => input.Round == 1
            ? Cuts(Cut(SpecRegisterNames.Steps, Step, NewStep)) : Cuts(Cut(SpecRegisterNames.Steps, NewStep, Third));
        rig.Replies(_ => SpecReply(Yaml(Lone, step: NewStep), "Applied the first cut."), _ => "not a proposal at all");

        var reply = await rig.RunAsync();

        reply.Response.Text.Should().StartWith("Applied the first cut.").And.Contain(NewStep);
        rig.DraftOf(Lone).Yaml.Should().Contain(NewStep);
    }

    [Fact]
    public async Task ProposalReview_StoppedDraftRewrittenWithoutCuts_IsReverted()
    {
        var rig = new ProposalReviewRig();
        rig.Propose(Epic());
        rig.OnReview = input => input.Draft.SpecId == First && input.Round == 1
            ? RefuseCuts(Cut(SpecRegisterNames.Steps, Step, NewStep))
            : input.Draft.SpecId == First ? Cuts(Cut(SpecRegisterNames.Steps, NewStep, Step)) : Build();
        rig.Replies(
            _ => Epic(Yaml(First, step: NewStep), Yaml(Second, requires: [First])),
            _ => Epic(Yaml(First), Yaml(Second, "A rewrite of a stopped draft.", requires: [First])));

        await rig.RunAsync();

        rig.DraftOf(Second).Goal.Should().NotBe("A rewrite of a stopped draft.");
        rig.Nudges[1].Should().NotContain($"## {Second}\n", "a stopped draft is not asked for anything");
    }

    [Fact]
    public async Task ProposalReview_DraftUnchangedAfterRePrompt_StopsWithResidues()
    {
        var rig = new ProposalReviewRig();
        rig.Propose(Epic());
        rig.OnReview = input => input.Round == 1 ? RefuseCuts(Cut(SpecRegisterNames.Steps, Step, NewStep)) : Build();
        rig.Replies(_ => Epic(Yaml(First), Yaml(Second, step: NewStep, requires: [First])));

        await rig.RunAsync();

        var first = rig.Outcome.ReviewRounds.Where(r => r.SpecId == First).ToList();
        first.Should().ContainSingle("a draft a round left unchanged is not reviewed again in this turn");
        first[0].Residues.Should().ContainSingle().Which.Quote.Should().Be(Step);
        rig.Outcome.ReviewRounds.Count(r => r.SpecId == Second).Should().Be(2);
    }

    [Fact]
    public async Task ProposalReview_ReProposalChangedNothing_EndsTheLoop()
    {
        var rig = new ProposalReviewRig();
        rig.Propose(SpecReply(Yaml(Lone)));
        rig.OnReview = _ => Cuts(Cut(SpecRegisterNames.Steps, Step, NewStep));
        rig.Replies(_ => SpecReply(Yaml(Lone), "Nothing to change."));

        var reply = await rig.RunAsync();

        rig.Nudges.Should().ContainSingle();
        rig.Reviewed.Should().HaveCount(2);
        reply.Should().BeSameAs(rig.FirstReply);
    }

    [Fact]
    public async Task ProposalReview_ResidueAfterRePrompt_IsRecordedOnTheRound()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 1) };
        rig.Propose(SpecReply(Yaml(Lone)));
        rig.OnReview = _ => Cuts(Cut(SpecRegisterNames.Steps, Step, NewStep), Cut(SpecRegisterNames.Done, Done, "Every order lands in the file."));
        rig.Replies(_ => SpecReply(Yaml(Lone, step: NewStep)));

        await rig.RunAsync();

        rig.Outcome.ReviewRounds.Single().Residues.Should().ContainSingle()
            .Which.Should().Be(new SpecReviewResidue(SpecRegisterNames.Done, Done));
    }

    [Fact]
    public async Task ProposalReview_RoundKeepsItsReviewerVerdictsAndFindings()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 2) };
        rig.Propose(SpecReply(Yaml(Lone)));
        rig.OnReview = input => input.Round == 2 ? Build()
            : input.Reviewer == 0 ? Refuse("the writer is not buffered") : Cuts(Cut(SpecRegisterNames.Steps, Step, NewStep));
        rig.Replies(_ => SpecReply(Yaml(Lone, step: NewStep)));

        await rig.RunAsync();

        var first = rig.Outcome.ReviewRounds[0];
        first.Verdict.Should().Be(SpecReviewVerdicts.Refuse);
        first.ReviewerVerdicts.Should().Equal(SpecReviewVerdicts.Refuse, SpecReviewVerdicts.BuildWithCuts);
        first.Findings.Should().ContainSingle().Which.Text.Should().Be("the writer is not buffered");
        rig.Outcome.Findings.Should().NotContain(f => f.Why == "the writer is not buffered",
            "a REFUSE a later round cleared stays on its round, not among what still stands");
    }

    [Fact]
    public async Task ProposalReview_EpicDrafts_ReviewedConcurrentlyUnderMaxConcurrent()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 1, reviewers: 2, maxConcurrent: 3), GatherInFlight = 3 };
        rig.Propose(Epic());

        await rig.RunAsync();

        rig.Reviewed.Should().HaveCount(4, "two reviewers on each of two children");
        rig.MaxInFlight.Should().Be(3, "the drafts' calls run together, never more than max_concurrent at once");
    }

    [Fact]
    public async Task ProposalReview_StoppedDraftUnchanged_IsNotReviewedAgain()
    {
        var rig = new ProposalReviewRig();
        rig.Propose(Epic());
        rig.OnReview = input => input.Draft.SpecId == Second && input.Round < 3
            ? Cuts(Cut(SpecRegisterNames.Steps, input.Round == 1 ? Step : NewStep, input.Round == 1 ? NewStep : Step)) : Build();
        rig.Replies(
            _ => Epic(Yaml(First), Yaml(Second, step: NewStep, requires: [First])),
            _ => Epic(Yaml(First), Yaml(Second, requires: [First])));

        await rig.RunAsync();

        rig.Reviewed.Count(i => i.Draft.SpecId == First).Should().Be(2, "BUILD in round 1 stopped it");
    }

    [Fact]
    public async Task ProposalReview_StandingRefuse_IsAFinding_NothingHandedBack()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 1) };
        rig.Propose(SpecReply(Yaml(Lone)));
        rig.OnReview = _ => Refuse("the order store does not page");
        rig.Replies(_ => SpecReply(Yaml(Lone)));

        await rig.RunAsync();

        rig.Outcome.Findings.Should().ContainSingle(f => f.Problem == SpecReviewVerdicts.Refuse)
            .Which.Why.Should().Be("the order store does not page");
        rig.Outcome.NotReviewed.Should().BeNull("a standing REFUSE is shown, never held back from approval");
        rig.Pipeline.TryGet<object>(Contracts.Commands.ContextKeys.SpecSequenceStop, out _).Should().BeFalse();
    }

    [Fact]
    public async Task ProposalReview_ReviewerFails_BlocksNothing()
    {
        var rig = new ProposalReviewRig { OnReview = _ => throw new InvalidOperationException("model unreachable") };
        rig.Propose(SpecReply(Yaml(Lone)));

        var reply = await rig.RunAsync();

        rig.Outcome.ReviewRounds.Should().BeEmpty();
        rig.Outcome.NotReviewed.Should().BeNull();
        reply.Should().BeSameAs(rig.FirstReply);
    }
}
