using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Series;

/// <summary>
/// 2026-11-27-3532a: how a ticket a tracker triggered enters its series — it becomes a series of one, or its trigger reaches the
/// series that holds it, whose master is woken to start it (start_ticket). The trigger itself starts no run: every ticket has
/// one master, and the master decides which of its tickets start.
/// </summary>
public interface ISeriesTicketEntry
{
    /// <summary>Opens the ticket's series of one or finds the series that holds it and wakes its master; answers the sentence of
    /// what the series does with it. Null when this process keeps no series (the trigger then claims as it always did).</summary>
    Task<string?> EnterAsync(ResolvedProject project, string ticketId, CancellationToken cancellationToken);
}
