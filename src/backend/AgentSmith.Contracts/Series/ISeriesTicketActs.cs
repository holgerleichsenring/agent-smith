namespace AgentSmith.Contracts.Series;

/// <summary>
/// 2026-11-17-0207a: what the series master does to a member ticket, through the paths a person's act takes — each
/// answer is the sentence of what happened. Start is a 'start' run of a never-worked ticket whose requirements are
/// met; retry is a code run's Retry; tell is a message in the ticket's conversation, which a running attempt reads with
/// its next tool result and the ticket's steward answers otherwise.
/// </summary>
public interface ISeriesTicketActs
{
    Task<string> StartAsync(string project, string ticketId, CancellationToken cancellationToken);

    Task<string> RetryAsync(string project, string ticketId, string principal, CancellationToken cancellationToken);

    Task<string> TellAsync(
        string project, string ticketId, string text, string principal, bool mayStartRuns,
        CancellationToken cancellationToken);
}
