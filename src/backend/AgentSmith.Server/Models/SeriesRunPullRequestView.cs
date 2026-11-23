namespace AgentSmith.Server.Models;

/// <summary>2026-11-23-31dcb: a member ticket's pull request in one repo, from the PR review records — the open one, else
/// the newest — with its CI on its head (one of CiStates; null before a CI read on it) and a red head's first failed check
/// with its log link.</summary>
/// <param name="Target">The branch it targets when known; the records hold it only once merged.</param>
public sealed record SeriesRunPullRequestView(
    string Repo, string Number, string Url, string State, string? Ci, string? CiFailure, string? CiLog, string? Target);
