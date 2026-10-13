using AgentSmith.Contracts.Runs;
using AgentSmith.Server.Services.SpecDialog.Continuation;
using FluentAssertions;
using static AgentSmith.Server.Services.SpecDialog.Continuation.TicketContinuationEffects;
using static AgentSmith.Server.Services.SpecDialog.Continuation.TicketStartArms;

namespace AgentSmith.Tests.SpecDialog.Continuation;

/// <summary>2026-10-13-9a59m: the start arm an approval in a ticket's own conversation takes, per state.</summary>
public sealed class OperatorStartArmTests
{
    [Theory]
    [InlineData(Continue, true, null, RunStatuses.WaitingForInput, false, true, QuestionPending, "r-1")]
    [InlineData(Continue, false, "r-1", "running", false, false, WithoutRunsControl, null)]
    [InlineData(Continue, true, "r-1", "running", false, false, WaitForRun, "r-1")]
    [InlineData(Continue, true, null, RunStatuses.Failed, true, false, WaitForRun, null)]
    [InlineData(Continue, true, null, RunStatuses.WaitingForInput, false, false, AnswersHandback, "r-1")]
    [InlineData(Continue, true, null, RunStatuses.Failed, false, false, StartNow, null)]
    [InlineData(AmendNeverWorked, true, "r-claim", null, false, false, StartNow, null)]
    [InlineData(AmendNeverWorked, false, null, null, false, false, WithoutRunsControl, null)]
    public void OperatorStartArm_EachState_NamesItsArm(
        string effect, bool mayStart, string? live, string? status, bool queued, bool checkpointed, string arm, string? runId)
    {
        var inputs = new TicketStartInputs(mayStart, live, status is null ? null : "r-1", status, queued, checkpointed);

        var start = OperatorStartArm.Of(inputs, effect);

        start.Should().Be(new TicketContinuationStart(arm, runId));
        OperatorStartArm.Say(start!).Should().NotBeNullOrWhiteSpace();
        OperatorStartArm.Of(inputs, FollowUpClosed).Should().BeNull("only continue and amend-never-worked start");
    }

    [Fact]
    public void OperatorStartArm_QuestionPending_NamesTheAnswerPath()
    {
        OperatorStartArm.Say(new TicketContinuationStart(QuestionPending, "r-7"))
            .Should().Contain("POST /api/runs/r-7/answer").And.Contain("nothing was recorded");
    }
}
