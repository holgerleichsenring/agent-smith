using AgentSmith.Contracts.Runs;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-10-08-7c0e: the ticket's newest code attempt — the newest Run row of the code pipeline for
/// (project, ticket), queued reservations ignored, <paramref name="excludingRunId"/> left out when
/// given. Null when there is none, or the composition has no database.
/// </summary>
public interface IPreviousAttemptReader
{
    Task<PreviousAttempt?> LatestAsync(
        string project, string ticketId, string? excludingRunId, CancellationToken cancellationToken);

    /// <summary>2026-10-08-0781: whether a code run for the ticket waits in the capacity queue — a
    /// run that will start, and whose end will check the ticket again.</summary>
    Task<bool> HasQueuedAsync(string project, string ticketId, CancellationToken cancellationToken) =>
        Task.FromResult(false);
}
