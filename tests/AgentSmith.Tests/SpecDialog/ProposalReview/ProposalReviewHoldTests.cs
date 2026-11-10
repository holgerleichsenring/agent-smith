using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using FluentAssertions;
using Microsoft.Extensions.AI;
using static AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>2026-11-10-18cf: every draft of a design turn's proposal is attacked, or it is marked and says why.
/// 2026-11-10-8e59: only a build on the exact current text is a clean review.</summary>
public sealed class ProposalReviewHoldTests
{
    private const string Step = "Write the orders through the buffered file writer.";
    private const string NewStep = "Write the orders through the streaming file writer.";

    [Fact]
    public async Task ProposalReviewLoop_BothReviewersFail_DraftIsMarkedWithTheNoAnswerReason()
    {
        var rig = new ProposalReviewRig { OnReview = input => input.Draft.SpecId == Second ? null : Build() }.WithRepository();
        rig.Propose(Epic());

        await rig.RunAsync();

        rig.Outcome.NotReviewed.Should().Equal(Second);
        rig.Outcome.Findings.Should().Contain(f => f.SpecId == Second
            && f.Why == ProposalReviewSummary.NotReviewedWhy(ProposalReviewSummary.NoAnswerWhy));
        rig.Reviewed.Count(i => i.Draft.SpecId == Second).Should().Be(8, "each of the 4 rounds tries it again");
    }

    [Fact]
    public async Task ProposalReviewLoop_ReviewersFailThenAnswer_DraftIsReviewed()
    {
        var rig = new ProposalReviewRig
        {
            OnReview = input => input.Draft.SpecId == Second && input.Round == 1 ? null : Build(),
        }.WithRepository();
        rig.Propose(Epic());

        await rig.RunAsync();

        rig.Outcome.NotReviewed.Should().BeNull();
        rig.Outcome.ReviewRounds.Should().Contain(r => r.SpecId == Second && r.Round == 2);
    }

    [Fact]
    public async Task ProposalReview_LastRoundRefuseRewritten_IsMarkedWithoutTheOldFindings()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 1), OnReview = _ => Refuse("the writer is not buffered") };
        rig.Propose(SpecReply(Yaml(Lone)));
        rig.Replies(_ => SpecReply(Yaml(Lone, step: NewStep)));

        await rig.RunAsync();

        rig.DraftOf(Lone).Yaml.Should().Contain(NewStep);
        rig.Outcome.NotReviewed.Should().Equal(Lone);
        rig.Outcome.Findings.Should().Contain(f => f.SpecId == Lone
            && f.Why == ProposalReviewSummary.NotReviewedWhy(ProposalReviewSummary.LastRevisionWhy));
        rig.Outcome.Findings.Should().NotContain(f => f.Why == "the writer is not buffered",
            "that finding was about the text the rewrite replaced");
    }

    [Fact]
    public async Task ProposalReview_CostCapReachedBeforeTheRePrompt_MarkedWithTheCapReason()
    {
        var rig = new ProposalReviewRig();
        rig.Propose(SpecReply(Yaml(Lone)));
        rig.OnReview = _ =>
        {
            var tracker = PipelineCostTracker.GetOrCreate(rig.Pipeline);
            tracker.ApplyCostCap(new CostCapValues { Usd = 10m, Tokens = 10 });
            tracker.Track(new ChatResponse(new ChatMessage(ChatRole.Assistant, "x"))
            {
                Usage = new UsageDetails { InputTokenCount = 1_000, OutputTokenCount = 10 },
            });
            return RefuseCuts(Cut(SpecRegisterNames.Steps, Step, NewStep));
        };

        await rig.RunAsync();

        rig.Nudges.Should().BeEmpty("the cap stops the re-prompt");
        rig.Outcome.NotReviewed.Should().Equal(Lone);
        rig.Outcome.Findings.Should().Contain(f => f.SpecId == Lone && f.Problem == ProposalReviewSummary.NotReviewedYet
            && f.Why.Contains("cost cap"));
    }

    [Fact]
    public async Task ProposalReview_InvalidSettings_ReviewsNothingAndMarksTheDrafts()
    {
        var invalid = new ProposalReviewRig();
        invalid.Propose(Epic());
        await invalid.RunAsync(Reader(() => Of(rounds: -1)));

        var unreadable = new ProposalReviewRig();
        unreadable.Propose(SpecReply(Yaml(Lone)));
        await unreadable.RunAsync(Reader(() => throw new InvalidOperationException("the store is down")));

        invalid.Reviewed.Should().BeEmpty("a block the run refuses is never clamped to defaults");
        invalid.Outcome.NotReviewed.Should().Equal([First, Second], "an epic's parent is never reviewed, so it is not marked");
        invalid.Outcome.Findings.Should().Contain(f => f.SpecId == First && f.Why.Contains("spec_review.rounds is -1"));
        unreadable.Reviewed.Should().BeEmpty();
        unreadable.Outcome.Findings.Should().Contain(f => f.SpecId == Lone && f.Why.Contains("the store is down"));
    }

    [Fact]
    public async Task ProposalReview_BuildOnTheCurrentText_IsRecordedAsReviewedClean()
    {
        var rig = new ProposalReviewRig();
        rig.Propose(SpecReply(Yaml(Lone)));

        await rig.RunAsync();

        rig.Outcome.ReviewedClean.Should().Equal(ReviewedText.Of(Lone, rig.DraftOf(Lone).Yaml));
    }

    [Fact]
    public async Task ProposalReview_CutsAppliedAfterTheLastRound_AreNotReviewedClean()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 1), OnReview = _ => Cuts(Cut(SpecRegisterNames.Steps, Step, NewStep)) };
        rig.Propose(SpecReply(Yaml(Lone)));
        rig.Replies(_ => SpecReply(Yaml(Lone, step: NewStep)));

        await rig.RunAsync();

        rig.DraftOf(Lone).Yaml.Should().Contain(NewStep);
        rig.Outcome.ReviewedClean.Should().BeEmpty("no reviewer attacked the text the cuts produced");
    }

    [Fact]
    public async Task ProposalReview_CarriedDraftUnchanged_KeepsItsCleanReview()
    {
        var rig = new ProposalReviewRig();
        var earlier = rig.Propose(Epic());
        var first = ProposalSpecSet.Drafts(earlier).Single(d => d.SpecId == First);
        rig.Pipeline.Set(ContextKeys.SpecDialogRevisedProposal, earlier with
        {
            ReviewRounds = [new ProposalReviewRound(First, 1, SpecReviewVerdicts.Build, [SpecReviewVerdicts.Build], [], [], [])],
            NotReviewed = [Second],
            ReviewedClean = [ReviewedText.Of(First, first.Yaml)],
        });

        await rig.RunAsync();

        rig.Reviewed.Select(i => i.Draft.SpecId).Distinct().Should().Equal(Second);
        rig.Outcome.ReviewedClean.Select(r => r.SpecId).Should().BeEquivalentTo([First, Second]);
    }
}
