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
}
