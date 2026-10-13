using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Server.Services.Rework;

namespace AgentSmith.Server.Contracts;

/// <summary>
/// 2026-10-08-e8b9b: the ONE door a rework act goes through, from the ticket or a pull request:
/// decide whether the act asks for a new attempt, make the ticket claimable, start one attempt.
/// </summary>
public interface IReworkEntry
{
    Task<ReworkOutcome> EnterAsync(
        ResolvedProject project, string ticketId, ReworkAct act, string pipeline, CancellationToken cancellationToken);

    /// <summary>A pull-request act newer than a hand-back's waiting attempt (no checkpoint): the park is
    /// checked with the hand-back's status accepted, the waiting row is closed as superseded, the rework
    /// starts.</summary>
    Task<ReworkOutcome> AnswerHandbackAsync(
        ResolvedProject project, string ticketId, PreviousAttempt waiting, string pipeline, CancellationToken cancellationToken);

    /// <summary>2026-10-13-9a59t: an act on a ticket a person closed while its attempt waits (a hand-back or a
    /// checkpointed question): the waiting row is closed as superseded and ClosedForWork answered; null when
    /// the ticket is not closed for work.</summary>
    Task<ReworkOutcome?> SupersedeIfClosedAsync(
        ResolvedProject project, string ticketId, PreviousAttempt waiting, CancellationToken cancellationToken);
}
