using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// Which comments on a ticket are ours, which of ours are still the operator's turn, and
/// whether the operator took that turn.
/// <para>
/// p0448: the second question is the one that matters. A cancelled run, a failed run, the
/// cut we announced, a handback verdict — each reports what happened and asks for nothing.
/// Only an open question, an expectation to ratify, a question hand-back, a
/// contradiction hand-back and a scope-unassigned hand-back are waiting on a person, and only those have a reason to
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
    public const string WorkingStatus = "Agent Smith — working on this issue…";
    private static readonly string[] Ours =
    [
        "agent-smith:open-questions",
        "[agent-smith open questions]",
        "Agent Smith —",
        "Agent Smith &#8212;",
        // 2026-10-08-e8b9b: the completed-run summary heads itself with a hyphen.
        "Agent Smith - ",
        "Agent Smith is working on this issue",
    ];

    private static readonly string[] AwaitingAnswer =
    [
        "agent-smith:open-questions",
        "open questions",
        "expectation to ratify",
        Specs.SpecHandbackComment.QuestionMarker,
        Specs.SpecHandbackComment.ContradictionMarker,
        Specs.SpecHandbackComment.ScopeUnassignedMarker,
        Specs.SeriesOutOfStepComment.Marker, // 2026-10-12-f5f7f
        Specs.Review.ReviewObjectionComment.Marker, // 2026-10-13-8762h
    ];

    public static bool IsOurs(TicketComment comment) => Carries(comment, Ours);

    /// <summary>2026-10-08-e8b9a: the same test on a bare body, for a webhook that has no
    /// comment record yet — a comment of ours is never a trigger, so it cannot loop.</summary>
    public static bool IsOurs(string? body) => Ours.Any(marker =>
        body?.Contains(marker, StringComparison.OrdinalIgnoreCase) == true);

    public static bool AwaitsAnswer(TicketComment comment) => Carries(comment, AwaitingAnswer);

    /// <summary>True when someone other than us commented after our last comment carrying <paramref name="marker"/>
    /// — on the ticket, or on one of its pull requests (<paramref name="prNotes"/>).</summary>
    public static bool IsAnswered(
        IReadOnlyList<TicketComment>? comments, string marker, IReadOnlyList<PrReviewNote>? prNotes = null) =>
        ForeignAfter(comments, marker, prNotes).Any();

    /// <summary>
    /// 2026-09-22-8b25: the comments <see cref="IsAnswered"/> counts — everything by somebody
    /// other than us that follows our last comment carrying <paramref name="marker"/>, oldest
    /// first. Extracted because a second rule asks what those comments SAY, and two readings of
    /// one thread can disagree about which comments are even in scope.
    /// <para>The trusted notes on the ticket's pull requests count beside the ticket's comments, dated
    /// against the same anchor: a reviewer who answers a hand-back on the pull request has answered it.</para>
    /// </summary>
    public static IEnumerable<TicketComment> ForeignAfter(
        IReadOnlyList<TicketComment>? comments, string marker, IReadOnlyList<PrReviewNote>? prNotes = null)
    {
        var ordered = (comments ?? []).OrderBy(c => c.CreatedAt).ToList();
        var asked = ordered.LastOrDefault(c => IsOurs(c) && Carries(c, [marker]));
        var thread = ordered.Concat((prNotes ?? []).Where(n => !n.IsOurs).Select(AsComment));
        return thread.Where(c => (asked is null || c.CreatedAt > asked.CreatedAt) && !IsOurs(c)).OrderBy(c => c.CreatedAt);
    }

    /// <summary>The trusted pull-request notes this run read (ContextKeys.PrReviewFeedback), every thread flattened.</summary>
    public static IReadOnlyList<PrReviewNote> PrNotes(PipelineContext pipeline) =>
        pipeline.TryGet<IReadOnlyList<PrReviewFeedback>>(ContextKeys.PrReviewFeedback, out var feedback) && feedback is not null
            ? [.. feedback.SelectMany(f => f.Threads).SelectMany(t => t.Notes)]
            : [];

    private static TicketComment AsComment(PrReviewNote note) =>
        new(note.Author?.AuthorLogin ?? string.Empty, note.At, note.Body);

    private static bool Carries(TicketComment comment, string[] markers) =>
        markers.Any(marker =>
            comment.Body?.Contains(marker, StringComparison.OrdinalIgnoreCase) == true);
}
