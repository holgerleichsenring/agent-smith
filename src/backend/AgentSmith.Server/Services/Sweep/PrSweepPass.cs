using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sweep;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Server.Services.Webhooks;

namespace AgentSmith.Server.Services.Sweep;

/// <summary>
/// 2026-10-08-10b0: one repository's pull-request events in one cycle. A repository not yet recorded,
/// or unswept for two intervals, is recorded (labels, comments up to now) and starts nothing.
/// Afterwards a pull request without a row is new — its comments are read from its creation and a
/// review label already on it is scanned; a label going on is scanned; a newer comment goes to command
/// admission. Each start follows a compare-and-set that won. 2026-10-13-0b82a: opening a pull request
/// or pushing to it starts no review — a review starts on a person's command, which the comments carry.
/// A complete list prunes the rows of pull requests no longer open; a list the budget cut prunes nothing.
/// The repository's pull-request records are compared with the list for merges, closes and reopens.
/// </summary>
public sealed class PrSweepPass(PrSweepStore store, PrSweepActions actions, PrTriggerLabelResolver labels, PrSweepRecordNudge records)
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
            else await ActAsync(target, key, pr, states, ct);
        if (fresh) await new PrSweepComments(store, actions).RunAsync(target, key, page.Items, lister, turn, ct);
        if (!page.Cut) await store.PruneAsync(key, [.. page.Items.Select(p => p.Number)], ct);
        await records.RunAsync(target, key, page, resume, ct);
        await store.MarkSweptAsync(key, fresh || !page.Cut, now, ct);
        return page.Resume;
    }

    private async Task ActAsync(
        SweepTarget target, string key, OpenPullRequest pr, IReadOnlyDictionary<string, PrSweepState> states, CancellationToken ct)
    {
        if (!states.TryGetValue(pr.Number, out var row))
        {
            if (await store.TryInsertAsync(Row(target, key, pr, pr.CreatedAt), ct) && Labelled(target, pr))
                await actions.ScanAsync(target, pr);
            return;
        }
        var labelled = Labelled(target, pr);
        if (labelled != row.LabelPresent && await store.TrySetLabelAsync(key, pr.Number, row.LabelPresent, labelled, ct) && labelled)
            await actions.ScanAsync(target, pr);
    }

    private bool Labelled(SweepTarget target, OpenPullRequest pr) =>
        target.Repo.Type != RepoType.AzureDevOps && labels.Match(target.Config, PrSweepActions.Kind(target.Repo), target.Repo.Url!, pr.Labels) is not null;

    private PrSweepState Row(SweepTarget target, string key, OpenPullRequest pr, DateTimeOffset seen) => new()
    {
        Repository = key, Number = pr.Number, LabelPresent = Labelled(target, pr), CommentsSeenTicks = seen.UtcTicks,
    };
}
