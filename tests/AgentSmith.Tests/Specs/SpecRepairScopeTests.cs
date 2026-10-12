using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0444: a spec begins without the previous spec's repair hanging off it.
/// <para>
/// FOUND BY THE AGENT, MID-RUN. On live run 9a30, spec c's FIRST master pass was handed
/// p0438's repair block carrying spec B's outstanding criteria — "this is a REPAIR pass …
/// close exactly these … the rest of the spec is already accounted for, adding more is
/// scope you were not given" — while the tree still held every MassTransit reference spec
/// c existed to remove. It did not obey; it asked, and said why: "the repair instruction
/// says the rest of the spec is already accounted for, but the actual tree contradicts
/// that assertion."
/// </para>
/// <para>
/// The second half is worse than the misleading prompt: SpecRepairAttempted carried over
/// too, so spec c had already spent the single repair p0438 grants it — on spec b's
/// behalf. A spec would reach its verdict with no repair left.
/// </para>
/// </summary>
public sealed class SpecRepairScopeTests
{
    [Fact]
    public void EnteringASpec_LeavesNoRepairStateFromTheLastOne()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.OutstandingCriteria, new List<string> { "spec b's criterion" });
        pipeline.Set(ContextKeys.SpecRepairAttempted, true);

        SpecRepairScope.Reset(pipeline);

        pipeline.TryGet<List<string>>(ContextKeys.OutstandingCriteria, out _).Should().BeFalse(
            "a fresh spec is not repairing anything yet");
        pipeline.TryGet<bool>(ContextKeys.SpecRepairAttempted, out _).Should().BeFalse(
            "each spec gets its own single repair, not the remains of the last one's");
    }

    /// <summary>The block is what the master reads; with the state gone it says nothing.</summary>
    [Fact]
    public void AFreshSpec_IsToldNothingAboutAnEarlierRepair()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.OutstandingCriteria, new List<string> { "spec b's criterion" });
        SpecExecutionPromptBlocks.OutstandingCriteria(pipeline).Should().Contain("REPAIR pass");

        SpecRepairScope.Reset(pipeline);

        SpecExecutionPromptBlocks.OutstandingCriteria(pipeline).Should().BeEmpty();
    }

    [Fact]
    public void ResettingAPipelineThatNeverRepaired_ChangesNothing()
    {
        var pipeline = new PipelineContext();

        var reset = () => SpecRepairScope.Reset(pipeline);

        reset.Should().NotThrow();
        pipeline.TryGet<bool>(ContextKeys.SpecRepairAttempted, out _).Should().BeFalse();
    }
}
