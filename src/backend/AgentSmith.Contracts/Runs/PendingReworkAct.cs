using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-10-08-0781: the newest rework act after an attempt's cutoff, with where to answer it — the
/// ticket, or the pull request (its repository and URL) a review stands on.
/// </summary>
public sealed record PendingReworkAct(ReworkAct Act, RepoConnection? Repo = null, string? PrUrl = null);
