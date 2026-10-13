namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59b: one pull request of a ticket as its record says — the list row's and the
/// detail header's PR cell.
/// </summary>
/// <param name="State">Exactly open, merged, closed, unsupported or unread.</param>
/// <param name="Decision">Exactly one of <see cref="TicketReviewDecisions"/>, from the standing decisions only.</param>
/// <param name="By">The login of the reviewer whose decision is shown; null for none.</param>
/// <param name="LastActivityAt">The newest entry, else when the pull request was opened, else <paramref name="StateAt"/>.</param>
/// <param name="StateAt">When the host says the state began; null before any read.</param>
/// <param name="Note">What the decision means for the ticket where that is not obvious.</param>
public sealed record TicketPullRequestView(
    string Number,
    string Url,
    string Repo,
    string State,
    string Decision,
    string? By,
    DateTimeOffset? LastActivityAt,
    DateTimeOffset? StateAt,
    string? Note);
