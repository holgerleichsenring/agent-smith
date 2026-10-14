using AgentSmith.Contracts.Decisions;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-10-13-8762p: the decided defaults of a ticket — those of its code runs, plus those of the design
/// conversation bound to it when <paramref name="conversation"/> names one. Empty when the composition has no
/// database.
/// </summary>
public interface IDecidedDefaults
{
    Task<IReadOnlyList<DecidedDefault>> ReadAsync(
        string project, string ticketId, string? conversation, CancellationToken cancellationToken);
}
