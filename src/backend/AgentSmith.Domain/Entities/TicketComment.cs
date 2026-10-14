using AgentSmith.Domain.Models;

namespace AgentSmith.Domain.Entities;

/// <summary>
/// One entry of a ticket's comment thread — the conversation that happened on the
/// tracker after the ticket was written. Author-attributed and timestamped so the
/// prompt can render the thread chronologically; the body is ticket-origin text and
/// therefore UNTRUSTED (rendered inside the p0316 delimiters, never obeyed).
/// </summary>
public sealed record TicketComment(
    string Author,
    DateTimeOffset CreatedAt,
    string Body)
{
    /// <summary>
    /// The tracker's own id of the comment, or null where the payload carried none. A cause
    /// recorded from a keyword comment stores it as its source id.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>One of <see cref="TicketTextFormat"/>: what the tracker returned the body as.</summary>
    public string Format { get; init; } = TicketTextFormat.Text;
}
