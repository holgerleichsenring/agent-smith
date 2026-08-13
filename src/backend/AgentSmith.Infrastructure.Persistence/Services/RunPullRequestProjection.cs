using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0347: a PR outcome lands in TWO durable places — the per-repo RunRepo row
/// (feeds the run-snapshot PrUrl + beats) and the Runs.PullRequestsJson list (the
/// durable, timestamped, multi-repo-complete history the Pull Requests page + run
/// detail read). Same event, one apply.
/// <para>
/// p0405: split out of <see cref="RunEventApplier"/> like
/// <see cref="RunCheckpointProjection"/> — the applier routes an event, this owns
/// what a run's pull requests mean across both places they are recorded.
/// </para>
/// </summary>
public sealed class RunPullRequestProjection
{
    public async Task ApplyAsync(IUnitOfWork uow, PullRequestOutcomeEvent e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        await UpsertRepoAsync(uow, e, ct);
        await UpsertJsonAsync(uow, e, ct);
    }

    private static async Task UpsertRepoAsync(
        IUnitOfWork uow, PullRequestOutcomeEvent e, CancellationToken ct)
    {
        var repo = await uow.Set<RunRepo>()
            .FirstOrDefaultAsync(r => r.RunId == e.RunId && r.RepoName == e.Repo, ct);
        if (repo is null) { repo = new RunRepo { RunId = e.RunId, RepoName = e.Repo }; uow.Add(repo); }
        repo.PrUrl = e.Url; repo.PrStatus = e.Status; repo.Reason = e.Reason;
        await uow.SaveChangesAsync(ct);
    }

    // p0347: fold the outcome into the run's PullRequestsJson list, upserting by
    // repo (the last outcome per repo wins — a retried commit/PR step overwrites
    // the earlier attempt). The stored camelCase JSON IS the wire payload the
    // dashboard reads, matching the p0344b run-story pattern.
    private static async Task UpsertJsonAsync(
        IUnitOfWork uow, PullRequestOutcomeEvent e, CancellationToken ct)
    {
        var run = await uow.Set<Run>().FirstOrDefaultAsync(r => r.Id == e.RunId, ct);
        if (run is null) return;
        var prs = RunStoryJson.TryDeserialize<List<RunPullRequestView>>(run.PullRequestsJson)
            ?? new List<RunPullRequestView>();
        prs.RemoveAll(p => p.Repo == e.Repo);
        prs.Add(new RunPullRequestView(e.Repo, e.Status, e.Url, e.Reason, e.Timestamp));
        run.PullRequestsJson = RunStoryJson.Serialize(prs);
        await uow.SaveChangesAsync(ct);
    }
}
