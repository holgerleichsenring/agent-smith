namespace AgentSmith.Contracts.Turns;

/// <summary>
/// 2026-11-17-4860b: what a ticket's conversation addressed to a running code attempt — the operator messages the
/// conversation stamped with that run's id instead of running a turn for them. Registered by the server, which holds
/// the conversations; a process without it (the CLI) delivers nothing.
/// </summary>
public interface ITicketConversationMessages
{
    /// <summary>When the ticket's conversation last changed; null when the ticket has none. Cheap: no transcript read.</summary>
    Task<DateTimeOffset?> LastActivityAsync(string project, string ticketId, CancellationToken cancellationToken);

    /// <summary>The messages addressed to <paramref name="runId"/> stored after transcript index
    /// <paramref name="afterIndex"/>, oldest first.</summary>
    Task<IReadOnlyList<RunAddressedMessage>> AddressedToAsync(
        string project, string ticketId, string runId, int afterIndex, CancellationToken cancellationToken);
}

/// <summary>2026-11-17-4860b: one operator message addressed to a run, at its transcript index.</summary>
public sealed record RunAddressedMessage(int Index, string Text, DateTimeOffset At);
