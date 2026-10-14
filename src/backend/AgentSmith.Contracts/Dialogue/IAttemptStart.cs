namespace AgentSmith.Contracts.Dialogue;

/// <summary>
/// Starting the next attempt of the ticket a conversation is bound to, as the person who posted the turn.
/// Built only for a ticket that has an attempt and a poster who may start runs, so a turn holding this port
/// can always ask. Asking queues the start; the rework worker can still decline and says so on the ticket.
/// </summary>
public interface IAttemptStart
{
    /// <summary>The sentence the conversation is told: requested, or why not.</summary>
    /// <param name="reason">What the person said changed — kept in the log beside the request.</param>
    Task<string> StartNextAsync(string reason, CancellationToken cancellationToken);
}
