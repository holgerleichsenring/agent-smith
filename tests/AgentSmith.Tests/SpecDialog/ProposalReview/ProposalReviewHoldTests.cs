using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Microsoft.Extensions.AI;
using static AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>2026-11-10-18cf: every draft of a design turn's proposal is attacked, or it is held and says why.</summary>
public sealed class ProposalReviewHoldTests
{
    private const string Step = "Write the orders through the buffered file writer.";
    private const string NewStep = "Write the orders through the streaming file writer.";

    [Fact]
    public async Task ProposalReviewLoop_BothReviewersFail_DraftIsHeldWithTheNoAnswerReason()
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
    public async Task ProposalReview_LastRoundRefuseRewritten_IsHeldWithoutTheOldFindings()
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
    public async Task ProposalReview_CostCapReachedBeforeTheRePrompt_HeldWithTheCapReason()
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
    public async Task ProposalReviewLoop_BlindAfterACancelledTurn_StillHolds()
    {
        var rig = new ProposalReviewRig { OnReview = input => input.Draft.SpecId == Second ? Blind() : Build() };
        var earlier = rig.Propose(Epic());
        rig.Pipeline.Set(ContextKeys.SpecDialogRevisedProposal, earlier with
        {
            UnderReview = true,
            NotReviewed = [First, Second],
            Findings = [.. new[] { First, Second }.Select(id => new ProposalFinding(id, ProposalReviewSummary.NotReviewedYet,
                ProposalReviewSummary.NotReviewedWhy(ProposalReviewSummary.UnderReviewWhy)))],
        });

        await rig.RunAsync();

        rig.Outcome.NotReviewed.Should().Equal([Second], "a cancelled turn never tried to read it; the blind exit needs a try");
    }

    [Fact]
    public async Task ProposalReview_InvalidOrUnreadableSettings_ReviewsInRoundsWithTheDefaults()
    {
        var invalid = new ProposalReviewRig();
        invalid.Propose(SpecReply(Yaml(Lone)));
        await invalid.RunAsync(Reader(() => Of(rounds: -1)));

        var unreadable = new ProposalReviewRig();
        unreadable.Propose(SpecReply(Yaml(Lone)));
        await unreadable.RunAsync(Reader(() => throw new InvalidOperationException("the store is down")));

        invalid.Outcome.ReviewRounds.Should().ContainSingle(r => r.SpecId == Lone, "the default block reviews in rounds");
        unreadable.Outcome.ReviewRounds.Should().ContainSingle(r => r.SpecId == Lone);
    }

    private static SpecAttackResult Blind() => new(SpecReviewVerdicts.Build, [], []) { Blind = true };
}
