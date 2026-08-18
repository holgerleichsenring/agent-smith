using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0444: a repair belongs to ONE phase, and ends with it.
/// <para>
/// p0438 records a repair in the pipeline bag — the outstanding criteria the master must
/// close, and the flag saying its single repair is spent. Nothing cleared either when the
/// next phase began, so the bag is where one phase's repair leaked into the next.
/// </para>
/// <para>
/// Found by the agent, mid-run: on 9a30 phase c's FIRST pass was handed phase b's
/// outstanding criteria as a repair instruction — "close exactly these, the rest of the
/// phase is already accounted for" — while the tree still held everything phase c existed
/// to change. It refused to obey a contradiction and asked instead. The quieter half is
/// worse: the spent-repair flag carried over too, so phase c had already used the one
/// repair it was owed, on another phase's behalf.
/// </para>
/// </summary>
public static class PhaseRepairScope
{
    /// <summary>Forget any repair the previous phase was in the middle of.</summary>
    public static void Reset(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        pipeline.Remove(ContextKeys.OutstandingCriteria);
        pipeline.Remove(ContextKeys.PhaseRepairAttempted);
    }
}
