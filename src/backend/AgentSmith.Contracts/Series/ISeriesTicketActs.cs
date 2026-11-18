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

    /// <summary>2026-11-18-3863h: a 'deploy' run through the entry a person's deploy takes, for an environment the
    /// binding delivers without approval only; listed on <paramref name="ticketId"/> when one is named.</summary>
    Task<string> DeployAsync(
        string project, string context, string environment, string? version, string? ticketId, CancellationToken cancellationToken);
}
