using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using FluentAssertions;
using static AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>
/// 2026-11-11-2bdfe: a design turn's review stops once a used revision lays new open questions on the operator, and
/// publishes its drafts as waiting on those answers instead of reviewing them further.
/// </summary>
public sealed class ProposalReviewWaitsOnAnswersTests
{
    private const string Step = "Write the orders through the buffered file writer.";
    private const string NewStep = "Write the orders through the streaming file writer.";
    private static readonly OpenQuestion Earlier = Question("Does the export keep cancelled orders?", "Leave them out.");
    private static readonly OpenQuestion Added = Question("Who owns the file?", "Accounting.");

    [Fact]
    public async Task ProposalReview_RevisionAddsQuestion_ReviewEndsAndOpenDraftsWaitOnAnswers()
    {
        var rig = new ProposalReviewRig { OnReview = _ => RefuseCuts(Cut(SpecRegisterNames.Steps, Step, NewStep)) };
        rig.Slot.Questions = [Earlier];
        rig.Propose(SpecReply(Yaml(Lone)));
        rig.Replies(rig.Asks(_ => SpecReply(Yaml(Lone, step: NewStep)), Earlier, Added));

        await rig.RunAsync();

        rig.Nudges.Should().ContainSingle("no round follows the revision that asked the operator");
        rig.Reviewed.Should().OnlyContain(i => i.Round == 1);
        rig.DraftOf(Lone).Yaml.Should().Contain(NewStep, "the revision is published");
        rig.Outcome.NotReviewed.Should().Equal(Lone);
        rig.Outcome.Findings.Should().Contain(f => f.SpecId == Lone && f.Problem == ProposalReviewSummary.NotReviewedYet
            && f.Why == ProposalReviewSummary.NotReviewedWhy(ProposalReviewSummary.WaitingOnAnswersWhy(2)));
    }

    [Fact]
    public async Task ProposalReview_RevisionKeepsQuestions_NextRoundRuns()
    {
        var rig = new ProposalReviewRig
        {
            OnReview = input => input.Round == 1 ? RefuseCuts(Cut(SpecRegisterNames.Steps, Step, NewStep)) : Build(),
        };
        rig.Slot.Questions = [Earlier];
        rig.Propose(SpecReply(Yaml(Lone)));
        rig.Replies(rig.Asks(_ => SpecReply(Yaml(Lone, step: NewStep)), Question("  Does the export keep cancelled orders? ", "Keep them flagged.")));

        await rig.RunAsync();

        rig.Reviewed.Should().Contain(i => i.Round == 2, "the same question, trimmed, is not a new one");
        rig.Outcome.NotReviewed.Should().BeNull();
    }

    [Fact]
    public async Task ProposalReview_RevisionAddsQuestion_StoppedDraftsKeepTheirRecord()
    {
        var rig = new ProposalReviewRig
        {
            OnReview = input => input.Draft.SpecId == First ? RefuseCuts(Cut(SpecRegisterNames.Steps, Step, NewStep)) : Build(),
        };
        rig.Propose(Epic());
        rig.Replies(rig.Asks(_ => Epic(Yaml(First, step: NewStep), Yaml(Second, requires: [First])), Added));

        await rig.RunAsync();

        rig.Outcome.NotReviewed.Should().Equal(First);
        rig.Outcome.ReviewRounds.Where(r => r.SpecId == Second).Should().ContainSingle()
            .Which.Verdict.Should().Be(SpecReviewVerdicts.Build, "the draft that stopped keeps its clean round");
        rig.Outcome.ReviewedClean.Should().Contain(t => t.SpecId == Second);
        rig.Outcome.Findings.Should().NotContain(f => f.SpecId == Second && f.Problem == ProposalReviewSummary.NotReviewedYet);
    }
}
