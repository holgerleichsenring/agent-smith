using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207a: a member ticket by the key or the id the master names it with.</summary>
public static class SeriesTicketLookup
{
    public static SeriesTicketFact? Find(SeriesBeat beat, string ticket)
    {
        ArgumentNullException.ThrowIfNull(beat);
        var name = (ticket ?? string.Empty).Trim().TrimStart('#');
        return beat.Facts.FirstOrDefault(f => string.Equals(f.Ticket.Key.TrimStart('#'), name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(f.Ticket.TicketId, name, StringComparison.OrdinalIgnoreCase));
    }

    public static string Unknown(SeriesBeat beat, string ticket) =>
        $"'{ticket}' is not a ticket of this series; its tickets are {string.Join(", ", beat.Facts.Select(f => f.Ticket.Key))}.";
}
