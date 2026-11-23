using AgentSmith.Contracts.Runs;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-11-22-9719d: what an attempt's own pull-request record says — moved out of <see cref="DbPreviousAttemptReader"/>
/// so the full and the end-only attempt reads judge "opened" by one rule.
/// </summary>
internal static class AttemptPullRequests
{
    public static IReadOnlyDictionary<string, string> AwaitingCi(string? json) =>
        (json is null ? [] : RunStoryJson.TryDeserialize<List<RunPullRequestView>>(json) ?? [])
            .Where(p => p.Status == PullRequestStatuses.AwaitingCi && !string.IsNullOrEmpty(p.Url))
            .GroupBy(p => p.Repo).ToDictionary(g => g.Key, g => g.Last().Url!);

    public static bool OpenedAny(string? json) =>
        json is not null && (RunStoryJson.TryDeserialize<List<RunPullRequestView>>(json) ?? []).Any(Opened);

    /// <summary>2026-11-23-97c2b: the attempt's pull request in the repo still stands on its branch — open, awaiting CI, left
    /// open by a refused completion, or 2026-11-23-31dca armed to merge itself, which it has not done yet. A completed one
    /// merged, and a host may have deleted its branch.</summary>
    public static bool Stands(RunPullRequestView pr) =>
        (PullRequestStatuses.IsOpen(pr.Status) || pr.Status is PullRequestStatuses.CompletionRefused or PullRequestStatuses.CompletionArmed)
        && !string.IsNullOrEmpty(pr.Url);

    public static bool Opened(RunPullRequestView pr) => PullRequestStatuses.IsOpen(pr.Status) && !string.IsNullOrEmpty(pr.Url); // 3435b
}
