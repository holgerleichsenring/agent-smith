namespace AgentSmith.Contracts.Series;

/// <summary>
/// 2026-11-17-0207a: what the series master does to a member ticket, through the paths a person's act takes — each
/// answer is the sentence of what happened. Start is a 'start' run of a never-worked ticket whose requirements are
/// met; retry is a code run's Retry; tell is a message in the ticket's conversation, which a running attempt reads with
/// its next tool result and the ticket's steward answers otherwise.
/// </summary>
public interface ISeriesTicketActs
{
    /// <summary>2026-11-26-5f6e: start_ticket's start, answered as what it came to.</summary>
    Task<SeriesStart> StartAsync(string project, string ticketId, CancellationToken cancellationToken);

    Task<string> RetryAsync(string project, string ticketId, string principal, CancellationToken cancellationToken);

    /// <summary>2026-11-27-3532b: answers the ticket's parked run, else writes into its conversation.</summary>
    Task<string> TellAsync(SeriesTell tell, CancellationToken cancellationToken);

    /// <summary>2026-11-18-3863h: a 'deploy' run through the entry a person's deploy takes, for an environment the
    /// binding delivers without approval only; listed on <paramref name="ticketId"/> when one is named.</summary>
    Task<string> DeployAsync(
        string project, string context, string environment, string? version, string? ticketId, CancellationToken cancellationToken);

    /// <summary>2026-11-19-1b72a: a resume's start — as <see cref="StartAsync"/>, but a ticket whose requirement waits for a
    /// start by name is left to that start, and the answer says whether it started.</summary>
    Task<SeriesStart> StartIfReadyAsync(string project, string ticketId, CancellationToken cancellationToken);

    /// <summary>2026-11-24-f9c5e: wakes the tickets outside the series that wait on its tickets, now that the series reached
    /// the default branch; answers how many were woken.</summary>
    Task<int> WakeDependentsAsync(string project, IReadOnlyList<string> ticketIds, CancellationToken cancellationToken);

    /// <summary>2026-11-19-1b72a: cancels a member ticket's unfinished run through the run cancel a person's Cancel takes.</summary>
    Task<string> CancelAsync(string runId, CancellationToken cancellationToken);
}
