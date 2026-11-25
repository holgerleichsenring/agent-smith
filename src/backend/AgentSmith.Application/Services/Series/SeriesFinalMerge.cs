using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Series;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-24-f9c5e: merges a series' final pull request under merge.when in the project's protection mode — manual waits for
/// a person, on_gate_green merges (the series check was the gate), on_ci_green merges on green CI or on none for
/// merge.ci_settle_seconds and says a red one. The merge takes the steps every merge path takes (completion; 2026-11-25-9992h: no approval, a required review is a person's). A
/// conflict — at a merge or in the host's state — goes to the one conflict refresh (2026-11-24-297b), which brings the series
/// branch up to date with its base once per conflict.
/// </summary>
public sealed class SeriesFinalMerge(
    ISourceProviderFactory sources, PullRequestCompleter completer, ISeriesRunLaunch launch,
    PullRequestBuildRequeue? requeue = null)
{
    public async Task<SeriesDeliveryStep> MergeAsync(DeliverSeriesContext context, RepoConnection repo, string branch,
        SeriesDelivery delivery, string? head, DateTimeOffset now, CancellationToken ct)
    {
        var policy = context.Project.Merge;
        if (policy.When == MergeWhen.Manual)
            return SeriesDeliveryStep.Of(delivery, "waits for a person", $"{delivery.Url} waits for your merge into the default branch.");
        if (head is not null && delivery.ArmedHead == head) return SeriesDeliveryStep.Of(delivery, "armed", null); // the host completes it
        if (policy.When == MergeWhen.OnCiGreen && await CiAsync(repo, delivery, head, policy, now, ct) is { } waits) return waits;
        var entry = new OpenedPullRequest(repo.Name, delivery.Url, OpenStatus.Opened);
        var completion = await completer.CompleteAsync(context.Pipeline, repo, entry, new BranchName(branch), policy.Protection, ct);
        return completion.Outcome switch
        {
            PullRequestCompletionOutcome.Merged => SeriesDeliveryStep.Of(delivery, "merged", $"{delivery.Url} merged: {repo.Name} has the series.", merged: true),
            PullRequestCompletionOutcome.Armed => SeriesDeliveryStep.Of(delivery with { ArmedHead = head }, "armed",
                $"{delivery.Url} completes once the host's rules pass: {completion.Reason}"),
            _ => await RefusedAsync(context, repo, branch, delivery, head, completion, now, ct),
        };
    }

    // Null when the CI lets the merge go ahead; otherwise what the pull request waits for.
    private async Task<SeriesDeliveryStep?> CiAsync(RepoConnection repo, SeriesDelivery delivery, string? head, MergePolicyConfig policy,
        DateTimeOffset now, CancellationToken ct)
    {
        var ci = sources.Create(repo) is IPullRequestCiReader reader
            ? await reader.ReadCiAsync(delivery.Url, new CiReadRequest(head, policy.CiExcerptChars, policy.CiFailedChecksRead), ct)
            : PullRequestCi.NoneOn(head);
        if (ci.State == CiStates.Green) return null;
        if (ci.State == CiStates.Red)
            return SeriesDeliveryStep.Of(delivery, "CI red", $"{delivery.Url} is not merged: its CI is red ({ci.Failures.FirstOrDefault()?.Name ?? "a check failed"}).");
        if (ci.State == CiStates.NotStarted && head is not null && delivery.RequeuedHead != head && requeue is not null)
        {
            await requeue.RequeueAsync(repo, delivery.Url, ct); // a required build that never started on this head
            return SeriesDeliveryStep.Of(delivery with { RequeuedHead = head }, "build queued again", null);
        }
        if (ci.State != CiStates.None) return SeriesDeliveryStep.Of(delivery, "CI runs", null);
        var since = delivery.NoCiHead == head && delivery.NoCiSince is { } seen ? seen : now;
        var waited = delivery with { NoCiHead = head, NoCiSince = since };
        return now - since >= TimeSpan.FromSeconds(policy.CiSettleSeconds) ? null : SeriesDeliveryStep.Of(waited, "no CI yet", null);
    }

    private async Task<SeriesDeliveryStep> RefusedAsync(DeliverSeriesContext context, RepoConnection repo, string branch,
        SeriesDelivery delivery, string? head, PullRequestCompletion completion, DateTimeOffset now, CancellationToken ct)
    {
        if (!completion.Conflicts || head is null)
            return SeriesDeliveryStep.Of(delivery, "refused", $"{delivery.Url} is not merged: {completion.Reason}");
        return await RefreshAsync(context, repo, branch, delivery, head, now, ct);
    }

    /// <summary>2026-11-24-f9c5f: a final pull request that conflicts with its base — whether the host said so at a merge or in
    /// its state (2026-11-24-6811a) — handed to the conflict refresh, which starts one run per conflict on a head.</summary>
    public async Task<SeriesDeliveryStep> RefreshAsync(DeliverSeriesContext context, RepoConnection repo, string branch,
        SeriesDelivery delivery, string head, DateTimeOffset now, CancellationToken ct)
    {
        var since = delivery.ConflictHead == head && delivery.ConflictSince is { } seen ? seen : now;
        var conflicted = delivery with { ConflictHead = head, ConflictSince = since };
        return await launch.RefreshAsync(context.Project.Name, repo.Name, branch, delivery.Url, delivery.Number, since, ct)
            ? SeriesDeliveryStep.Of(conflicted, "conflicts", $"{delivery.Url} conflicts with its base; a refresh-base run brings {branch} up to date with it.")
            : SeriesDeliveryStep.Of(conflicted, "conflicts", null);
    }
}
