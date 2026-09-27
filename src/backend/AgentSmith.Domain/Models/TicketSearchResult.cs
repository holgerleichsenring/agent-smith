namespace AgentSmith.Domain.Models;

/// <summary>2026-09-27-5c1ea: what one ticket search is allowed to reveal — an identifier and a title.</summary>
/// <param name="Kind">2026-09-27-481bf: what the tracker calls it, in the tracker's own word.
/// One nullable field, riding a read each search already performs — affordable where a whole-ticket
/// read per hit would not be. Null where a tracker gives none, which is every GitHub issue.</param>
public sealed record TicketSearchHit(TicketId Id, string Title, string? Kind = null);

/// <summary>Whether the tracker ran the query at all.</summary>
public enum TicketSearchOutcome
{
    /// <summary>The query ran. <see cref="TicketSearchResult.Hits"/> is the answer, empty or not.</summary>
    Searched,

    /// <summary>The query did not run. Nothing is known about the board, and the reason says why.</summary>
    Failed
}

/// <summary>
/// 2026-09-27-5c1ea: a tracker's answer to a text search, following <see cref="TicketRewriteResult"/>.
/// <para>
/// THE TWO FLAGS ARE THE POINT. <see cref="MoreHeldBack"/> distinguishes a full screen from a whole
/// board, because a cap that does not say so is a lie; and <see cref="TicketSearchOutcome.Failed"/>
/// distinguishes a tracker that could not answer from a board with no such ticket, because the
/// existing list helpers collapse both into an empty list and the second reading is the dangerous
/// one — it tells a person their ticket does not exist.
/// </para>
/// </summary>
public sealed record TicketSearchResult(
    TicketSearchOutcome Outcome, IReadOnlyList<TicketSearchHit> Hits, bool MoreHeldBack, string? Reason)
{
    public bool Searched => Outcome == TicketSearchOutcome.Searched;

    /// <summary>The query ran and matched nothing — which is a fact about the board.</summary>
    public static TicketSearchResult None { get; } = new(TicketSearchOutcome.Searched, [], false, null);

    /// <summary>
    /// Keeps the first <paramref name="limit"/> of what the tracker returned and reports whether
    /// anything was dropped. Callers over-ask by one (or read the tracker's own total) so that
    /// "more existed" is measured rather than guessed.
    /// </summary>
    public static TicketSearchResult Of(IEnumerable<TicketSearchHit> hits, int limit, bool moreHeldBack = false)
    {
        var all = hits.ToList();
        return new(
            TicketSearchOutcome.Searched, all.Take(limit).ToList(), moreHeldBack || all.Count > limit, null);
    }

    public static TicketSearchResult Failed(string reason) =>
        new(TicketSearchOutcome.Failed, [], false, reason);
}
