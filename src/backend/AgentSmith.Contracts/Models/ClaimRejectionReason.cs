namespace AgentSmith.Contracts.Models;

/// <summary>
/// Reason a claim was rejected. Rejected claims will never succeed as-is —
/// operator must fix config or change the trigger label. Not retried by the reconciler.
/// </summary>
public enum ClaimRejectionReason
{
    UnknownProject,
    UnknownPipeline,
    PipelineNotLabelTriggered,

    /// <summary>
    /// 2026-09-18-c1a7: the ticket's last run could not move it out of its trigger status —
    /// the configured value is one this tracker will refuse again. Lifted by changing the
    /// tracker's or the project's configuration, which is the fix.
    /// </summary>
    TicketLastLeftUnmoved
}
