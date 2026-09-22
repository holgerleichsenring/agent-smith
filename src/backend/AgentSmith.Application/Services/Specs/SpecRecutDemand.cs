using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-22-8b25: whether somebody asked, in so many words, for an approved specification to be
/// cut again — the one deliberate door for a person who can reach the ticket and not the branch.
/// <para>
/// THE PREDICATE IS ITS OWN. <see cref="OwnTicketComment.IsAnswered"/> uses its marker only to
/// find OUR anchor comment and never inspects the foreign one's body, so asking it with the
/// demand phrase would find no anchor, treat the whole thread as "after", and read every ordinary
/// comment ever written as a demand. This anchors on the cut phrase exactly as the comment rule
/// does AND requires the foreign comment to carry the demand phrase.
/// </para>
/// <para>
/// A WHOLE LINE AT THE START, never a fragment found anywhere. Whether a comment is OURS is
/// decided by a heading fragment matched anywhere in the body, and the list carries that heading
/// in two dash encodings — direct evidence that trackers re-encode what they are given. A phrase
/// matched anywhere would therefore fire on a quoted notice on one tracker and be swallowed as
/// ours on another, with punctuation deciding. Opening a comment with the phrase on a line of its
/// own is something a person does on purpose and a quote-back cannot do by accident.
/// </para>
/// <para>
/// A DEMAND IS A RE-APPROVAL. The set the run publishes is read back off the branch by the next
/// run, and <see cref="SpecSetIndex.ApprovalOf"/> is what tells it the set was approved. A re-cut
/// published without one would silently lose the immunity after exactly one demand, so the demand
/// records a new approval — attributed to the person who wrote it, timestamped when they wrote
/// it. That timestamp is also what CLEARS the demand: a comment no newer than the approval on the
/// branch has already been acted on. The cut comment moves the anchor as well, but a re-cut that
/// hands back never posts one, and a demand that fires on every later run is the failure mode the
/// kept-set notice already documents for its own cause.
/// </para>
/// </summary>
public static class SpecRecutDemand
{
    /// <summary>The demand this run must act on, or null when nobody made one.</summary>
    public static TicketComment? From(PipelineContext pipeline, SpecSet? set)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return In(
            pipeline.TryGet<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments, out var c)
                ? c : null,
            set?.Approval);
    }

    /// <summary>
    /// The last demand in the thread that postdates <paramref name="approval"/>, or null. An
    /// unapproved set has no approval to measure against and is cleared by the anchor alone, the
    /// way the plain comment cause is.
    /// </summary>
    public static TicketComment? In(IReadOnlyList<TicketComment>? comments, SpecApproval? approval) =>
        OwnTicketComment.ForeignAfter(comments, SpecSetComment.CutMarker)
            .Where(Demands)
            .LastOrDefault(c => approval is null || c.CreatedAt > approval.At);

    /// <summary>True when the comment OPENS with <see cref="SpecSetComment.RecutDemand"/> on a
    /// line of its own — leading blank lines and surrounding whitespace ignored, nothing else.</summary>
    public static bool Demands(TicketComment comment)
    {
        ArgumentNullException.ThrowIfNull(comment);
        return Opening(comment.Body) is { } line
            && string.Equals(line, SpecSetComment.RecutDemand, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The approval a DEMAND records, or null for every other cause — the one question
    /// the source precedence asks, so the cause check and the attribution stay in one place.</summary>
    public static SpecApproval? ApprovalFor(string cause, SpecSet set, PipelineContext pipeline) =>
        string.Equals(cause, SpecRevisionCause.RecutDemand, StringComparison.Ordinal)
            ? ApprovalOf(From(pipeline, set))
            : null;

    /// <summary>
    /// The approval a demand records, so the re-cut set stays approved on the branch and the
    /// demand cannot fire twice. The conversation is EMPTY on purpose: there was no design
    /// conversation, and the existing readers render an unnamed one as "(unnamed)" rather than
    /// inventing a session that nobody can open.
    /// </summary>
    public static SpecApproval? ApprovalOf(TicketComment? demand) =>
        demand is null ? null : new SpecApproval(demand.CreatedAt, string.Empty, demand.Author);

    private static string? Opening(string? body) =>
        body?.Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0);
}
