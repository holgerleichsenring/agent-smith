namespace AgentSmith.Contracts.Commands;

/// <summary>2026-11-17-e061a: the steps of the runs that act on a ticket after it was filed.</summary>
public static partial class CommandNames
{
    /// <summary>Closes the ticket's named pull requests and the tracker ticket, and stores the close mark when asked.</summary>
    public const string CloseTicket = "CloseTicketCommand";

    /// <summary>Clears a ticket's close mark.</summary>
    public const string ReopenTicket = "ReopenTicketCommand";
}
