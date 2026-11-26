using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Series;

/// <summary>2026-11-23-31dcb: a ticket's pull requests as the series page shows them — per repo the open record, else the
/// newest, in the project's repo order, with the CI read on its head and a red head's first failed check.</summary>
public sealed class SeriesRunPullRequests
{
    public IReadOnlyList<SeriesRunPullRequestView> Of(IEnumerable<PrReviewRecordView> records, ResolvedProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var repos = project.Repos.Select(r => r.Name).ToList();
        return [.. records.GroupBy(r => r.Repo, StringComparer.Ordinal)
            .Select(g => g.LastOrDefault(r => r.State is PrReviewStates.Open or PrReviewStates.Unread) ?? g.Last())
            .OrderBy(r => repos.IndexOf(r.Repo) is var at && at < 0 ? int.MaxValue : at).ThenBy(r => r.Repo, StringComparer.Ordinal)
            .Select(View)];
    }

    /// <summary>The pull request's target is known and is neither its repo's configured default branch nor 2026-11-24-f9c5c
    /// the series' branch — the two places a ticket of the series merges.</summary>
    public bool TargetsElsewhere(SeriesRunPullRequestView pullRequest, ResolvedProject project, string? seriesBranch = null)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);
        if (pullRequest.Target is { } into && string.Equals(into, seriesBranch, StringComparison.Ordinal)) return false;
        var main = project.Repos.FirstOrDefault(r => r.Name == pullRequest.Repo)?.DefaultBranch;
        return pullRequest.Target is { } target && !string.IsNullOrEmpty(main) && !string.Equals(target, main, StringComparison.Ordinal);
    }

    private static SeriesRunPullRequestView View(PrReviewRecordView record)
    {
        var ci = PullRequestHeadCi.OnHead(record); // 2026-11-25-9992a: the hold's rule — an older head's read is pending
        var failed = ci?.State == CiStates.Red ? ci.Failures.FirstOrDefault() : null;
        return new SeriesRunPullRequestView(record.Repo, record.Number, record.Url, record.State, ci?.State,
            PullRequestHeadCi.FirstFailure(ci), failed?.Url, record.TargetBranch)
            { IsDraft = record.IsDraft, HasConflicts = record.HasConflicts, ApprovedBy = ApprovedBy(record), ApprovalRequired = record.ApprovalRequired }; // 8d16, 6811a, 9992i, 5201f
    }

    /// <summary>2026-11-25-9992i: the authors of the record's standing approvals, agent-smith's own left out.</summary>
    public static IReadOnlyList<string> ApprovedBy(PrReviewRecordView record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return [.. record.Entries
            .Where(e => e.Standing && !e.IsOurs && e.Decision is PrReviewDecisions.Approved or PrReviewDecisions.ApprovedWithSuggestions)
            .Select(e => e.AuthorLogin ?? "a reviewer").Distinct(StringComparer.Ordinal)];
    }
}
