using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// Which comments on a ticket are ours, which of ours are still the operator's turn, and
/// whether the operator took that turn.
/// <para>
/// p0448: the second question is the one that matters. A cancelled run, a failed run, the
/// cut we announced, a handback verdict — each reports what happened and asks for nothing.
/// Only an open question, an expectation to ratify, a question hand-back and a
/// contradiction hand-back are waiting on a person, and only those have a reason to
/// survive into the next run's reading of the ticket.
/// </para>
/// <para>
/// 2026-09-07-bd7a: "answered" is mechanical and read from the thread the run already
/// carries — the last of our comments carrying a marker, followed by any comment that is
/// not ours. The branch is never the signal: every derivation commits a fresh revision.
/// A thread holding comments by others but none of ours carrying the marker reads as
/// answered — the safe direction, since the model then sees the thread and decides again
/// at the cost of one park rather than the work.
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
        Specs.SpecHandbackComment.QuestionMarker,
        Specs.SpecHandbackComment.ContradictionMarker,
    ];

    public static bool IsOurs(TicketComment comment) => Carries(comment, Ours);

    public static bool AwaitsAnswer(TicketComment comment) => Carries(comment, AwaitingAnswer);

    /// <summary>True when someone other than us commented after our last comment carrying <paramref name="marker"/>.</summary>
    public static bool IsAnswered(IReadOnlyList<TicketComment>? comments, string marker)
    {
        if (comments is null || comments.Count == 0) return false;
        var ordered = comments.OrderBy(c => c.CreatedAt).ToList();
        var asked = ordered.LastOrDefault(c => IsOurs(c) && Carries(c, [marker]));
        var after = asked is null ? ordered : ordered.Where(c => c.CreatedAt > asked.CreatedAt);
        return after.Any(c => !IsOurs(c));
    }

    private static bool Carries(TicketComment comment, string[] markers) =>
        markers.Any(marker =>
            comment.Body?.Contains(marker, StringComparison.OrdinalIgnoreCase) == true);
}
