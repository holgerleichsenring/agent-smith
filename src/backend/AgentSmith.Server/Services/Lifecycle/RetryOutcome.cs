namespace AgentSmith.Server.Services.Lifecycle;

/// <summary>
/// 2026-09-21-1fa0: what an operator retry actually did. The retry used to answer a bool that
/// meant "a trigger status was configured" and was read as "the ticket moved", so the one
/// outcome an operator had to know about — the tracker would not make the move — was reported
/// as success. Three states, because there are three, and each is said out loud.
/// </summary>
public enum RetryOutcome
{
    /// <summary>The project declares no trigger status to move the ticket to. Nothing was touched.</summary>
    NoTriggerStatus,

    /// <summary>The tracker offered no move to that status. The ticket keeps its hold.</summary>
    TrackerRefusedTheMove,

    /// <summary>The ticket moved and the hold was dropped — the poller can claim it again.</summary>
    Retried,
}
