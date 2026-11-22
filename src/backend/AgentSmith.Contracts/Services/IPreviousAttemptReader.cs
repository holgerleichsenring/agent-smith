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

    /// <summary>2026-11-19-1b72a: whether the ticket has an attempt <see cref="LatestAsync"/> would answer — an existence
    /// read for the starts that only ask whether the ticket was worked.</summary>
    async Task<bool> HasAttemptAsync(string project, string ticketId, CancellationToken cancellationToken) =>
        await LatestAsync(project, ticketId, null, cancellationToken) is not null;

    /// <summary>2026-11-22-9719d: the tickets among <paramref name="ticketIds"/> that have an attempt, as
    /// <see cref="HasAttemptAsync"/> counts one — one read for many.</summary>
    async Task<IReadOnlySet<string>> AttemptedAsync(
        string project, IReadOnlyCollection<string> ticketIds, CancellationToken cancellationToken)
    {
        var attempted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ticketIds)
            if (await HasAttemptAsync(project, id, cancellationToken)) attempted.Add(id);
        return attempted;
    }

    /// <summary>2026-11-22-9719d: each ticket's newest attempt by ticket id, carrying only its own end — status, finished,
    /// <see cref="PreviousAttempt.StoppedAtBoundary"/>, <see cref="PreviousAttempt.OpenedPullRequest"/> — for the readers
    /// that judge a branch green; a ticket without an attempt is absent.</summary>
    async Task<IReadOnlyDictionary<string, PreviousAttempt>> LatestEndsAsync(
        string project, IReadOnlyCollection<string> ticketIds, CancellationToken cancellationToken)
    {
        var ends = new Dictionary<string, PreviousAttempt>(StringComparer.Ordinal);
        foreach (var id in ticketIds)
            if (await LatestAsync(project, id, null, cancellationToken) is { } attempt) ends[id] = attempt;
        return ends;
    }

    /// <summary>2026-10-08-0781: whether a code run for the ticket waits in the capacity queue — a
    /// run that will start, and whose end will check the ticket again.</summary>
    Task<bool> HasQueuedAsync(string project, string ticketId, CancellationToken cancellationToken) =>
        Task.FromResult(false);
}
