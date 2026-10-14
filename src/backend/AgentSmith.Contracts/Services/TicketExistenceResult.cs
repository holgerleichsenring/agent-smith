namespace AgentSmith.Contracts.Services;

/// <summary>The ids the tracker does not have, and the ids it could not be asked about with the class of why.</summary>
public sealed record TicketExistenceResult(IReadOnlyList<string> Missing, IReadOnlyList<UnreadableTicket> Unreadable)
{
    public static TicketExistenceResult None { get; } = new([], []);
}

/// <summary>A ticket id whose read failed for another reason than not-found; <paramref name="Reason"/> is a class
/// such as "unauthorized" or "unreachable".</summary>
public sealed record UnreadableTicket(string Id, string Reason);
