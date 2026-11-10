using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762h: the ticket comment a review-objection question posts, and the two facts the thread holds
/// about it. The heading carries <see cref="Marker"/>, which keeps the comment in the next run's view while it is
/// unanswered; the reason carries one <see cref="MarkerFor"/> line per objection, and that line on a comment of
/// ours is the fact that the objection was ASKED — nothing is stored twice. 2026-11-10-8e59: only an explicit
/// answer choosing the build reading lets the spec through; silence sends it to fresh review rounds.
/// </summary>
public static class ReviewObjectionComment
{
    /// <summary>The phrase that marks a review-objection comment as still awaiting an answer. Plain letters and
    /// spaces only: a tracker that stores a comment HTML-escaped (an apostrophe as &amp;#39;) must still match it.</summary>
    public const string Marker = "an objection from the spec review stands";

    /// <summary>The phrase naming one objection — a spec id, or "series:" and the prefix's first member.</summary>
    public static string MarkerFor(string key) => $"an objection from the spec review to {key} stands";

    /// <summary>The heading phrase a question hand-back of this kind carries.</summary>
    public static string HeadingOf(SpecHandback handback) =>
        handback?.QuestionKind == SpecQuestionKind.ReviewObjection ? Marker : SpecHandbackComment.QuestionMarker;

    public static string Build(SpecHandback handback)
    {
        ArgumentNullException.ThrowIfNull(handback);
        var readings = string.Join("\n", handback.Readings.Select(
            (reading, i) => $"- {SpecHandbackComment.ReadingLabel(i)} {reading}"));
        return $"## Agent Smith — {Marker}\n\n"
            + handback.Reason + "\n\n" + readings
            + "\n\nReply with the reading you mean, or edit the ticket, and move it back to a trigger status. "
            + $"Only a reply choosing {SpecHandbackComment.ReadingLabel(0)} builds the spec as it stands. "
            + "If nobody answers, nothing is built unreviewed: the next run reviews the spec again from fresh rounds, "
            + "and asks again if the objection still stands.";
    }

    /// <summary>2026-11-10-8e59: a person answered this objection with the build reading — "(a)" or its words —
    /// after our question, on the ticket or a pull request. Silence or any other answer is not that.</summary>
    public static bool BuildAsReviewedAnswered(PipelineContext pipeline, string markerKey)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var comments = pipeline.TryGet<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments, out var c) ? c : null;
        // An answer counts only after we asked about exactly this objection.
        return IsAsked(comments, markerKey)
            && OwnTicketComment.ForeignAfter(comments, MarkerFor(markerKey), OwnTicketComment.PrNotes(pipeline))
            .Any(answer => Says(answer.Body, SpecHandbackComment.ReadingLabel(0), ReviewObjectionQuestion.BuildAsReviewed)
                && !Says(answer.Body, SpecHandbackComment.ReadingLabel(1), ReviewObjectionQuestion.ChangeAsAsked));
    }

    private static bool Says(string? body, string label, string words) =>
        body is not null && (body.Contains(label, StringComparison.OrdinalIgnoreCase)
            || body.Contains(words, StringComparison.OrdinalIgnoreCase));

    /// <summary>True when a comment of ours names the objection with <paramref name="markerKey"/>.</summary>
    public static bool IsAsked(IReadOnlyList<TicketComment>? comments, string markerKey) =>
        (comments ?? []).Any(c => OwnTicketComment.IsOurs(c)
            && c.Body?.Contains(MarkerFor(markerKey), StringComparison.OrdinalIgnoreCase) == true);

    /// <summary>A review-objection comment of ours is on the ticket and nobody answered it there or on a pull
    /// request. A question that was never posted is neither asked nor unanswered.</summary>
    public static bool AskedAndUnanswered(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var comments = pipeline.TryGet<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments, out var c) ? c : null;
        return (comments ?? []).Any(comment => OwnTicketComment.IsOurs(comment)
                && comment.Body?.Contains(Marker, StringComparison.OrdinalIgnoreCase) == true)
            && !OwnTicketComment.IsAnswered(comments, Marker, OwnTicketComment.PrNotes(pipeline));
    }
}
