namespace AgentSmith.Contracts.Models;

/// <summary>
/// Reason a claim was rejected. Rejected claims will never succeed as-is —
/// operator must fix config or change the trigger label. Not retried by the reconciler.
/// </summary>
public enum ClaimRejectionReason
{
    UnknownProject,
    UnknownPipeline,
    PipelineNotLabelTriggered
}
