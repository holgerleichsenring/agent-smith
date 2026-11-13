using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Turns;
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
}
