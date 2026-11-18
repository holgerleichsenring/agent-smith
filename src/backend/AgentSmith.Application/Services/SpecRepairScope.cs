using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0444: a repair belongs to ONE spec, and ends with it.
/// <para>
/// p0438 records a repair in the pipeline bag — the item the master must close, and the flag
/// saying its single repair is spent. Nothing cleared either when the next spec began, so the
/// bag is where one spec's repair leaked into the next (run 9a30). 2026-11-18-c8e2c: a master's
/// report is the spec's account, so the previous spec's verdict ends here too.
/// </para>
/// </summary>
public static class SpecRepairScope
{
    /// <summary>Forget any repair and any report the previous spec left.</summary>
    public static void Reset(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        pipeline.Remove(ContextKeys.OutstandingCriteria);
        pipeline.Remove(ContextKeys.SpecRepairRed);
        pipeline.Remove(ContextKeys.MasterVerification);
        pipeline.Remove(ContextKeys.MasterVerificationBeforeRepair);
        // One structure correction per spec, so the next spec starts fresh.
        pipeline.Remove(ContextKeys.StructureFindings);
        pipeline.Remove(ContextKeys.StructureGateAttempted);
    }
}
