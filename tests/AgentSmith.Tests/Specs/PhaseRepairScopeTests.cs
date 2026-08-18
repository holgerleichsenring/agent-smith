using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0444: a phase begins without the previous phase's repair hanging off it.
/// <para>
/// FOUND BY THE AGENT, MID-RUN. On live run 9a30, phase c's FIRST master pass was handed
/// p0438's repair block carrying phase B's outstanding criteria — "this is a REPAIR pass …
/// close exactly these … the rest of the phase is already accounted for, adding more is
/// scope you were not given" — while the tree still held every MassTransit reference phase
/// c existed to remove. It did not obey; it asked, and said why: "the repair instruction
/// says the rest of the phase is already accounted for, but the actual tree contradicts
/// that assertion."
/// </para>
/// <para>
/// The second half is worse than the misleading prompt: PhaseRepairAttempted carried over
/// too, so phase c had already spent the single repair p0438 grants it — on phase b's
/// behalf. A phase would reach its verdict with no repair left.
/// </para>
/// </summary>
public sealed class PhaseRepairScopeTests
{
    [Fact]
    public void EnteringAPhase_LeavesNoRepairStateFromTheLastOne()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.OutstandingCriteria, new List<string> { "phase b's criterion" });
        pipeline.Set(ContextKeys.PhaseRepairAttempted, true);

        PhaseRepairScope.Reset(pipeline);

        pipeline.TryGet<List<string>>(ContextKeys.OutstandingCriteria, out _).Should().BeFalse(
            "a fresh phase is not repairing anything yet");
        pipeline.TryGet<bool>(ContextKeys.PhaseRepairAttempted, out _).Should().BeFalse(
            "each phase gets its own single repair, not the remains of the last one's");
    }

    /// <summary>The block is what the master reads; with the state gone it says nothing.</summary>
    [Fact]
    public void AFreshPhase_IsToldNothingAboutAnEarlierRepair()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.OutstandingCriteria, new List<string> { "phase b's criterion" });
        PhaseExecutionPromptBlocks.OutstandingCriteria(pipeline).Should().Contain("REPAIR pass");

        PhaseRepairScope.Reset(pipeline);

        PhaseExecutionPromptBlocks.OutstandingCriteria(pipeline).Should().BeEmpty();
    }

    [Fact]
    public void ResettingAPipelineThatNeverRepaired_ChangesNothing()
    {
        var pipeline = new PipelineContext();

        var reset = () => PhaseRepairScope.Reset(pipeline);

        reset.Should().NotThrow();
        pipeline.TryGet<bool>(ContextKeys.PhaseRepairAttempted, out _).Should().BeFalse();
    }
}
