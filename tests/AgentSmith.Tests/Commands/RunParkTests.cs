using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// p0440: OPERATOR QUESTION — "why is there no entry under Needs you, when I am the one who
/// has to answer here?"
/// <para>
/// Because two different things park a run and the status knew only one. Live run a98c
/// stopped on the master's mid-run question and was persisted as
/// <c>Status=success, Summary="Pipeline parked: awaiting_user_input"</c> — the header read
/// Done, the "Needs you" filter (which reads <c>waiting_for_input</c>) counted zero, and
/// the question sat on the ticket with nothing in the dashboard pointing at it.
/// </para>
/// </summary>
public sealed class RunParkTests
{
    [Fact]
    public void AMasterQuestion_ParksTheRun()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.OpenQuestionsAwaitingAnswer, true);

        RunPark.IsWaitingForOperator(pipeline).Should().BeTrue(
            "a run that asked the operator something is waiting for the operator");
    }

    [Fact]
    public void ACheckpointedDialogue_ParksTheRun()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.WaitingForInput, true);

        RunPark.IsWaitingForOperator(pipeline).Should().BeTrue();
    }

    [Fact]
    public void ARunNobodyIsWaitingOn_IsNotParked()
    {
        RunPark.IsWaitingForOperator(new PipelineContext()).Should().BeFalse();
    }

    /// <summary>A flag explicitly set false is not a park — absence and denial read alike.</summary>
    [Fact]
    public void AClearedFlag_IsNotAPark()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.OpenQuestionsAwaitingAnswer, false);
        pipeline.Set(ContextKeys.WaitingForInput, false);

        RunPark.IsWaitingForOperator(pipeline).Should().BeFalse();
    }
}
