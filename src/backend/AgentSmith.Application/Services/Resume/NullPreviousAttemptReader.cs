using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Resume;

/// <summary>2026-10-08-7c0e: the DB-free default — no run history, so no previous attempt.</summary>
public sealed class NullPreviousAttemptReader : IPreviousAttemptReader
{
    public Task<PreviousAttempt?> LatestAsync(
        string project, string ticketId, string? excludingRunId, CancellationToken cancellationToken) =>
        Task.FromResult<PreviousAttempt?>(null);
}
