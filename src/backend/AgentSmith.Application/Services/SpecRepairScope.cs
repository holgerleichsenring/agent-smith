using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0444: a repair belongs to ONE spec, and ends with it.
/// <para>
/// p0438 records a repair in the pipeline bag — the outstanding criteria the master must
/// close, and the flag saying its single repair is spent. Nothing cleared either when the
/// next spec began, so the bag is where one spec's repair leaked into the next.
/// </para>
/// <para>
/// Found by the agent, mid-run: on 9a30 spec c's FIRST pass was handed spec b's
/// outstanding criteria as a repair instruction — "close exactly these, the rest of the
/// spec is already accounted for" — while the tree still held everything spec c existed
/// to change. It refused to obey a contradiction and asked instead. The quieter half is
/// worse: the spent-repair flag carried over too, so spec c had already used the one
/// repair it was owed, on another spec's behalf.
/// </para>
/// </summary>
public static class SpecRepairScope
{
    /// <summary>Forget any repair the previous spec was in the middle of.</summary>
    public static void Reset(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        pipeline.Remove(ContextKeys.OutstandingCriteria);
        pipeline.Remove(ContextKeys.SpecRepairAttempted);
        // 2026-09-17-042eh: the review's fix pass is the same mechanism under a second name
        // and ends the same way. A finding, a snapshot or a spent fix pass carried into the
        // next spec is the leak p0444 closed for the repair, reopened.
        pipeline.Remove(ContextKeys.SpecReviewFindings);
        pipeline.Remove(ContextKeys.SpecReviewFixPass);
        pipeline.Remove(ContextKeys.SpecReviewReverted);
        pipeline.Remove(ContextKeys.SpecAccountsSnapshot);
        pipeline.Remove(ContextKeys.RunAccountsSnapshot);
    }
}
