using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Resume;

/// <summary>2026-10-13-8762p: the DB-free default — no rows, so no verdict is read or recorded.</summary>
public sealed class NullDecidedDefaults : IDecidedDefaults
{
    public Task<IReadOnlyList<DecidedDefault>> ReadAsync(
        string project, string ticketId, string? conversation, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DecidedDefault>>([]);
}
