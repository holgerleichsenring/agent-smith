using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Lifecycle;
using AgentSmith.Contracts.Sweep;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Server.Services.Webhooks;

namespace AgentSmith.Server.Services.Sweep;

/// <summary>
/// 2026-10-08-10b0: one repository's pull-request events in one cycle. A repository not yet recorded,
/// or unswept for two intervals, is recorded (heads, labels, comments up to now) and starts nothing.
/// Afterwards a pull request without a row is new — reviewed, scanned if labelled, its comments read
/// from its creation; a new head is reviewed; a label going on is scanned; a newer comment goes to
/// command admission. Each start follows a compare-and-set that won, so a head is launched for once
/// whatever its run's outcome. 2026-10-09-af10: a head moved to our own work commit, or a pull request
/// whose sweep reviews failed three times in a row, records the head and starts no review. A complete list prunes the rows
/// of pull requests no longer open; a list the budget cut prunes nothing.
/// </summary>
public sealed class PrSweepPass(PrSweepStore store, PrSweepActions actions, PrTriggerLabelResolver labels)
{
    public async Task<string?> RunAsync(SweepTarget target, IOpenPullRequestLister lister, string? resume,
        TimeSpan interval, DateTimeOffset now, int turn, CancellationToken ct)
    {
        var key = RepoKey.Of(target.Repo.Url!);
        var page = await lister.ListOpenAsync(resume, ChangeSweepCycle.PagesPerSource, ct);
        var known = await store.RepositoryAsync(key, ct);
        var fresh = known is { Initialised: true } && now.UtcTicks - known.LastSweptTicks <= (interval * 2).Ticks;
        var states = fresh ? await store.StatesAsync(key, ct) : null;
        foreach (var pr in page.Items)
            if (states is null) await store.RecordAsync(Row(target, key, pr, now), ct);
            else await ActAsync(target, key, pr, states, lister, ct);
        if (fresh) await new PrSweepComments(store, actions).RunAsync(target, key, page.Items, lister, turn, ct);
        if (!page.Cut) await store.PruneAsync(key, [.. page.Items.Select(p => p.Number)], ct);
        await store.MarkSweptAsync(key, fresh || !page.Cut, now, ct);
        return page.Resume;
    }

    private async Task ActAsync(
        SweepTarget target, string key, OpenPullRequest pr, IReadOnlyDictionary<string, PrSweepState> states,
        IOpenPullRequestLister lister, CancellationToken ct)
    {
        if (!states.TryGetValue(pr.Number, out var row))
        {
            if (!await store.TryInsertAsync(Row(target, key, pr, pr.CreatedAt), ct)) return;
            await actions.ReviewAsync(target, key, pr);
            if (Labelled(target, pr)) await actions.ScanAsync(target, pr);
            return;
        }
        if (pr.HeadSha != row.ReviewedHead && await store.TryMoveHeadAsync(key, pr.Number, row.ReviewedHead, pr.HeadSha, ct)
            && !PrSweepBreaker.Paused(row) && !await OursAsync(lister, pr, ct))
            await actions.ReviewAsync(target, key, pr);
        var labelled = Labelled(target, pr);
        if (labelled != row.LabelPresent && await store.TrySetLabelAsync(key, pr.Number, row.LabelPresent, labelled, ct) && labelled)
            await actions.ScanAsync(target, pr);
    }

    /// <summary>
    /// 2026-10-09-af10: a head that moved to a work commit agent-smith pushed is not new work. The head
    /// is recorded either way; an unreadable message reviews as before.
    /// </summary>
    private static async Task<bool> OursAsync(IOpenPullRequestLister lister, OpenPullRequest pr, CancellationToken ct)
    {
        if (pr.HeadSha is null) return false;
        try { return WipCommit.IsOurs(await lister.HeadCommitMessageAsync(pr.HeadSha, ct)); }
        catch (Exception) when (!ct.IsCancellationRequested) { return false; }
    }

    private bool Labelled(SweepTarget target, OpenPullRequest pr) =>
        target.Repo.Type != RepoType.AzureDevOps && labels.Match(target.Config, PrSweepActions.Kind(target.Repo), target.Repo.Url!, pr.Labels) is not null;

    private PrSweepState Row(SweepTarget target, string key, OpenPullRequest pr, DateTimeOffset seen) => new()
    {
        Repository = key, Number = pr.Number, ReviewedHead = pr.HeadSha, LabelPresent = Labelled(target, pr), CommentsSeenTicks = seen.UtcTicks,
    };
}
