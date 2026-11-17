using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Turns;
using Microsoft.Extensions.Logging.Abstractions;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>
/// 2026-11-13-3dc8: the review pass reports its Reviewing step as the turn's own, and every step taken inside the pass
/// under the "Review" frame, so a reviewer's answer never reads as the master's narration.
/// </summary>
public sealed class ProposalReviewPassTests
{
    [Fact]
    public async Task ReviewerSteps_ReportedUnderReviewName()
    {
        var accessor = TurnActivityRecorder.Silent();
        var recorder = new TurnActivityRecorder();
        using var observing = accessor.Observe(recorder);
        var rig = new TicketPlanReviewRig();
        rig.OnReviewerCall = () => accessor.ReportAsync(
            new TurnActivity(TurnActivityKind.Model, "stub-model", "{\"verdict\":\"BUILD\"}"), CancellationToken.None).GetAwaiter().GetResult();

        await rig.RunAsync(TicketPlanReviewRig.List(TicketPlanReviewRig.Ticket("order-api")), rig.Turn(accessor));

        recorder.Seen.Should().Contain(a => a.Kind == TurnActivityKind.Reviewing && a.Name == null);
        recorder.Seen.Where(a => a.Kind == TurnActivityKind.Model).Should().NotBeEmpty()
            .And.OnlyContain(a => a.Name == $"{ProposalReviewPass.ReviewName} › stub-model");
    }

    /// <summary>2026-11-17-5701a: a list whose tickets carry specs gets its plan review and an attack of each spec.</summary>
    [Fact]
    public async Task TicketListWithSpecs_IsPlanReviewedAndAttacked()
    {
        var plan = new TicketPlanReviewRig();
        plan.ReviewerAnswers.Enqueue(TicketPlanReviewRig.Finding("order-ui", "size"));
        var rig = new ProposalReviewRig
        {
            OnReview = input => input.Draft.SpecId == "order-page" ? ProposalReviewRig.Refuse("the page has no route") : ProposalReviewRig.Build(),
        };
        rig.Pipeline.Set(ContextKeys.SpecDialogOutcome, ProposalReviewRig.TicketSpecs());
        var review = new SpecDialogProposalReview(ProposalReviewRig.Checks(), ProposalReviewRig.Reader(() => rig.Settings),
            rig.Pass(plans: plan.Review()), NullLogger<SpecDialogProposalReview>.Instance);

        await rig.RunAsync(over: review);

        plan.ReviewerPrompts.Should().ContainSingle();
        rig.WholeSetCalls.Should().Be(0);
        rig.Reviewed.Select(i => (i.Draft.SpecId, i.WiderGoal)).Distinct().Should().BeEquivalentTo(
            [("order-state", "Title of order-api"), ("order-page", "Title of order-ui")]);
        rig.Outcome.ReviewRounds.Select(r => r.SpecId).Should().BeEquivalentTo(["order-state", "order-page"]);
        rig.Outcome.Findings.Should().Contain(f => f.SpecId == "order-ui" && f.Problem == "size")
            .And.Contain(f => f.SpecId == "order-page" && f.Why == "the page has no route");
        ProposalSpecSet.TicketOf(rig.Outcome, "order-page")!.Key.Should().Be("order-ui");
    }
}
