using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-10-08-0781: the newest rework act after an attempt's cutoff, with where to answer it — the
/// ticket, or the pull request (its repository and URL) a review stands on.
/// <para>2026-10-13-9a59t: Text — what the person wrote (the comment, or the review note's body); null for a
/// vote without text and for acts made elsewhere. A follow-up of a closed ticket carries it.</para>
/// </summary>
public sealed record PendingReworkAct(ReworkAct Act, RepoConnection? Repo = null, string? PrUrl = null, string? Text = null);
