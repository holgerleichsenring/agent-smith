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
    TicketLastLeftUnmoved,

    /// <summary>2026-10-13-8762g: the ticket's last attempt stopped at a boundary with specs left; only a
    /// person's start picks it up again.</summary>
    TicketStoppedAtBoundary,

    /// <summary>2026-11-16-f3fbb: the ticket's last attempt stopped on a precondition (preflight, image trust, bootstrap);
    /// a person's start picks it up again.</summary>
    TicketStuckOnPrecondition,

    /// <summary>2026-11-17-166aa: the ticket's last attempt failed or was cancelled on a project with no failed_status, so
    /// its tracker status was left as it was; a person's start (Retry, a reply) picks it up again.</summary>
    TicketLeftInPlace,

    /// <summary>2026-10-14-dbceh: a ticket it requires has not landed, or its filing did not finish. Unlike the
    /// reasons above, this refusal lifts by itself once the required tickets land.</summary>
    TicketRequiresUnmet,

    /// <summary>2026-11-19-1b72a: the ticket's series is paused and no run worked the ticket yet. Lifted by the series'
    /// resume, which starts the ticket again.</summary>
    SeriesPaused
}
