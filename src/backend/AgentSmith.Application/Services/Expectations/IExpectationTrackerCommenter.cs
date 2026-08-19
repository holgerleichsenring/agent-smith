using AgentSmith.Contracts.Expectations;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Expectations;

/// <summary>
/// p0328: posts the drafted expectation as a ticket comment (per-platform
/// template, OpenQuestions precedent) so tracker-side operators SEE the block
/// even when they ratify via dashboard/chat. Fail-soft: a comment failure must
/// not kill the negotiation — the dialogue transports remain.
/// </summary>
public interface IExpectationTrackerCommenter
{
    /// <remarks>
    /// p0454: takes the TICKET — ratification waits for a person, so the comment names
    /// the one it waits for.
    /// </remarks>
    Task PostAsync(
        TrackerConnection tracker, Ticket ticket, ExpectationDraft draft,
        CancellationToken cancellationToken);
}
