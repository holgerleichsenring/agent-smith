using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using Sources = AgentSmith.Server.Models.TicketConversationSources;

namespace AgentSmith.Server.Services.Tickets.Conversation;

/// <summary>
/// 2026-10-13-9a59h: puts a ticket's entries together. Thread comments become ticket entries — ours too
/// (the worker's later answers, a follow-up's cross-naming), never transcript turns. A comment of ours that
/// carries a hand-back heading and was posted within ten minutes after a recorded hand-back folds into it,
/// on the ticket and on a pull request alike: a tracker that re-encodes the body cannot re-encode the
/// heading's phrase. Entries without a run go under the newest attempt whose acts-read anchor precedes them
/// (never its start, which a resume overwrites), before the first attempt as 'before', and Work it out turns
/// after the last attempt's worked approval as 'now'.
/// </summary>
public static class ConversationAssembly
{
    public static readonly TimeSpan FoldWindow = TimeSpan.FromMinutes(10);

    /// <summary>The phrases a posted hand-back heading carries, one per case.</summary>
    private static readonly string[] HandbackHeadings =
    [
        SpecHandbackComment.QuestionMarker, SpecHandbackComment.ContradictionMarker, SpecHandbackComment.ScopeUnassignedMarker,
        SeriesOutOfStepComment.Marker, "not implementable as specified", "refused: the ticket asks for something that must not be done",
        "the approved specification is not on the ticket branch", Application.Services.Specs.Review.ReviewObjectionComment.Marker,
    ];

    public static IEnumerable<TicketConversationEntryView> Thread(IReadOnlyList<TicketComment> comments) =>
        comments.Select((c, i) => new TicketConversationEntryView(
            c.Id is { } id ? $"{Sources.Ticket}:{id}" : $"{Sources.Ticket}:at:{c.CreatedAt.UtcTicks}:{i}", c.Id, null,
            Sources.Ticket, TicketConversationKinds.Comment,
            OwnTicketComment.IsOurs(c) ? TicketConversationRoles.Agent : TicketConversationRoles.Person,
            c.Author, OwnTicketComment.IsOurs(c), c.CreatedAt, null, null, c.Body));

    /// <summary>Drops the posted copies of a recorded hand-back.</summary>
    public static IEnumerable<TicketConversationEntryView> Folded(IReadOnlyList<TicketConversationEntryView> entries)
    {
        var handbacks = entries.Where(e => e.Kind == RunConversationKinds.HandBack).Select(e => e.At).ToList();
        return entries.Where(e => !(e.IsOurs && e.Source is Sources.Ticket or Sources.PrReview
            && HandbackHeadings.Any(h => e.Text?.Contains(h, StringComparison.OrdinalIgnoreCase) == true)
            && handbacks.Any(at => e.At >= at && e.At <= at + FoldWindow)));
    }

    /// <summary>Places every entry without a run; run-keyed entries keep theirs.</summary>
    public static TicketConversationEntryView Placed(
        TicketConversationEntryView entry, IReadOnlyList<Run> attempts, DateTimeOffset? lastApprovalAt)
    {
        if (entry.AttemptRunId is not null) return entry;
        if (entry.Source == Sources.Conversation && lastApprovalAt is { } approved && entry.At > approved)
            return entry with { Group = TicketConversationGroups.Now };
        return attempts.LastOrDefault(a => Anchor(a) <= entry.At) is { } attempt
            ? entry with { AttemptRunId = attempt.Id }
            : entry with { Group = TicketConversationGroups.Before };
    }

    /// <summary>
    /// The entry that demanded a re-cut: the run recorded the demand as its worked approval — the comment's
    /// author and time, with no conversation — so the comment or pull-request note matching both is it.
    /// </summary>
    public static string? Demand(RunAttempt? attempt, IEnumerable<TicketConversationEntryView> entries) =>
        attempt is { RevisionCause: SpecRevisionCause.RecutDemand, ApprovalAt: { } at, ApprovalPrincipal: { } principal }
        && string.IsNullOrEmpty(attempt.ApprovalConversation)
            ? entries.FirstOrDefault(e => !e.IsOurs && e.Source is Sources.Ticket or Sources.PrReview
                && string.Equals(e.Author, principal, StringComparison.Ordinal)
                && (e.At - at).Duration() < TimeSpan.FromSeconds(1))?.Id
            : null;

    private static DateTimeOffset Anchor(Run attempt) => attempt.ActsReadAt ?? attempt.CreatedAt;
}
