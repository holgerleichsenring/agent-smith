using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-09-27-5c1ea: asking a tracker for the open tickets whose TITLE OR BODY matches a few
/// typed words — the one read a person drives by typing, rather than the framework by configuring.
/// <para>
/// A CAPABILITY PORT OF ITS OWN, for the reason <see cref="ITicketRewriter"/> already records and
/// this phase measured again: every one of the four provider classes sits at its file-length
/// baseline and may only get shorter, so a member all four must implement could land only by
/// splitting a type across files to get under a per-file limit. Four small implementations beside
/// them cost nothing.
/// </para>
/// <para>
/// IT DOES NOT ROUTE THROUGH THE EXISTING LIST HELPERS, which would defeat the point. All four of
/// those LOG their truncation and never return it, and all four swallow a failed query into an
/// empty list — so a picker built on them could not tell "your board has no such ticket" from
/// "this tracker could not run the query", nor "here is a screenful of many" from "here is all of
/// it". Those two distinctions are the whole reason this port exists.
/// </para>
/// </summary>
public interface ITicketSearch
{
    /// <summary>
    /// The open tickets whose title or body matches <paramref name="text"/>, most recently updated
    /// first, at most <paramref name="limit"/> of them — and, when the tracker could not run the
    /// query at all, a refusal carrying the reason rather than an empty list. Never throws.
    /// </summary>
    Task<TicketSearchResult> SearchAsync(string text, int limit, CancellationToken cancellationToken);
}
