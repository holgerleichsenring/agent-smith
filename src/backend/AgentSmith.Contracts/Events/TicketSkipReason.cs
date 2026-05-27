namespace AgentSmith.Contracts.Events;

/// <summary>
/// Discriminator for <see cref="TicketSkippedEvent"/>. Companion to the
/// free-form Detail field — the enum gives the dashboard a grouping
/// axis, the Detail string gives the operator the specific answer.
/// </summary>
public enum TicketSkipReason
{
    /// <summary>No trigger matched the ticket (label / status / project predicates).</summary>
    ZeroMatch = 0,
    /// <summary>A trigger matched the labels/keywords but the ticket status was not in trigger_statuses.</summary>
    StatusFilter = 1,
    /// <summary>Ticket already in-flight — claim service rejected the spawn.</summary>
    ClaimBlocked = 2,
    /// <summary>Same ticket already has an in-progress run (de-dup).</summary>
    DuplicateInFlight = 3,
    /// <summary>Source delivery was a pull request / merge request, not a ticket.</summary>
    NotAnIssue = 4,
}
