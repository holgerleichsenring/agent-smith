using AgentSmith.Contracts.Providers;

namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-11-23-31dce: what a merge did to one repository's pull request — the host's outcome (a pull request never tried
/// because an earlier one was not merged counts as refused) and the sentence the run's result says for it, which names
/// the repository and the host's or guard's reason. A merge-green run a person asked for hands these back to the page.
/// </summary>
public sealed record PullRequestMergeLine(string Repo, PullRequestCompletionOutcome Outcome, string Text);
