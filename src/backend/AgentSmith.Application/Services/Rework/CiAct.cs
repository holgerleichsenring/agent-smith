using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Application.Services.Rework;

/// <summary>
/// 2026-11-17-3435c: a red CI as an act on a merge: on_ci_green ticket, read from its pull-request records — the one
/// rule the rework worker and the run both apply. Either the newest attempt finished green and a pull request it left
/// waiting for CI went red on its head after the attempt's cutoff (the act's time is when the red was first read); or
/// the newest attempt was itself a CI fix that finished and left a red head unmoved (the act's time is its end), which
/// the run then hands back. The cutoff makes it idempotent: an attempt started after the red no longer precedes it.
/// </summary>
public static class CiAct
{
    public const string Actor = "ci";

    public static CiRedFinding? Of(ResolvedProject project, PreviousAttempt attempt, IReadOnlyList<PrReviewRecordView> records)
    {
        if (project.Merge.When != MergeWhen.OnCiGreen || !attempt.Finished) return null;
        var byUrl = records.ToDictionary(r => r.Url.Trim(), StringComparer.Ordinal);
        if (attempt.Status == RunStatuses.Success && Red(attempt.AwaitingCiUrls, byUrl, r => attempt.Precedes(r.Ci!.At!.Value)) is { Count: > 0 } red)
            return new CiRedFinding(Act(red.Max(r => r.Record.Ci!.At!.Value), red), [.. red.Select(r => r.Pr)], false);
        if (attempt.CauseChannel != AttemptChannels.Ci || attempt.Status == RunStatuses.WaitingForInput || attempt.FinishedAt is not { } end)
            return null;
        var unmoved = Red(attempt.PullRequestUrls, byUrl, r => r.HeadMovedAt is not { } moved || moved < attempt.StartedAt);
        return unmoved.Count > 0 ? new CiRedFinding(Act(end, unmoved), [.. unmoved.Select(r => r.Pr)], true) : null;
    }

    private static List<(CiRedPullRequest Pr, PrReviewRecordView Record)> Red(IReadOnlyDictionary<string, string> urls,
        IReadOnlyDictionary<string, PrReviewRecordView> byUrl, Func<PrReviewRecordView, bool> counts) =>
        [.. urls.Select(u => (Repo: u.Key, Record: byUrl.GetValueOrDefault(u.Value.Trim())))
            .Where(x => x.Record is { State: PrReviewStates.Open, Ci: { State: CiStates.Red, At: not null } ci } r && ci.IsOn(r.HeadSha) && counts(r))
            .Select(x => (new CiRedPullRequest(x.Repo, x.Record!.Url, x.Record.Ci!.Failures), x.Record!))];

    private static ReworkAct Act(DateTimeOffset at, IReadOnlyList<(CiRedPullRequest Pr, PrReviewRecordView Record)> red) =>
        new(Actor, at, ReworkChannel.Ci)
        {
            Source = new AttemptSource(AttemptSourceKinds.Ci, Url: red[0].Pr.Url,
                Detail: string.Join(", ", red.SelectMany(r => r.Pr.Failures.Select(f => f.Name)).Distinct())),
        };
}
