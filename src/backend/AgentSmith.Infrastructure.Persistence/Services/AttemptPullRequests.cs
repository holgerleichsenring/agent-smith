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

    public static bool Opened(RunPullRequestView pr) => PullRequestStatuses.IsOpen(pr.Status) && !string.IsNullOrEmpty(pr.Url); // 3435b
}
