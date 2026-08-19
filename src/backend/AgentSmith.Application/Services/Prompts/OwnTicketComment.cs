using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// Which comments on a ticket are ours, and which of ours are still the operator's turn.
/// <para>
/// p0448: the second question is the one that matters. A cancelled run, a failed run, the
/// cut we announced, a handback verdict — each reports what happened and asks for nothing.
/// Only an open question and an expectation to ratify are waiting on a person, and only
/// those have a reason to survive into the next run's reading of the ticket.
/// </para>
/// </summary>
public static class OwnTicketComment
{
    private static readonly string[] Ours =
    [
        "agent-smith:open-questions",
        "[agent-smith open questions]",
        "Agent Smith —",
        "Agent Smith &#8212;",
    ];

    private static readonly string[] AwaitingAnswer =
    [
        "agent-smith:open-questions",
        "open questions",
        "expectation to ratify",
    ];

    public static bool IsOurs(TicketComment comment) => Carries(comment, Ours);

    public static bool AwaitsAnswer(TicketComment comment) => Carries(comment, AwaitingAnswer);

    private static bool Carries(TicketComment comment, string[] markers) =>
        markers.Any(marker =>
            comment.Body?.Contains(marker, StringComparison.OrdinalIgnoreCase) == true);
}
