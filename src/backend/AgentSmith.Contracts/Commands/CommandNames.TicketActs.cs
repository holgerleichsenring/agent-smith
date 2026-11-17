namespace AgentSmith.Contracts.Commands;

/// <summary>2026-11-17-e061a: the steps of the runs that act on a ticket after it was filed.</summary>
public static partial class CommandNames
{
    /// <summary>Closes the ticket's named pull requests and the tracker ticket, and stores the close mark when asked.</summary>
    public const string CloseTicket = "CloseTicketCommand";

    /// <summary>Clears a ticket's close mark.</summary>
    public const string ReopenTicket = "ReopenTicketCommand";

    /// <summary>2026-11-17-e061b: makes an existing ticket claimable — its routing tag, then a move into a trigger status.</summary>
    public const string StartTicket = "StartTicketCommand";

    /// <summary>2026-11-17-e061c: rewrites a never-worked ticket's description, records its approved series, writes the branch.</summary>
    public const string AmendTicket = "AmendTicketCommand";

    /// <summary>2026-11-17-e061c: posts agent-smith's own comments on tickets.</summary>
    public const string PostTicketComments = "PostTicketCommentsCommand";
}
