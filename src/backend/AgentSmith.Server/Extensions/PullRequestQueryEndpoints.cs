using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0347: the dashboard's READ surface for agent-smith's OUTPUT — every PR the
/// agent opened, flattened across runs and joined to its run/ticket facts,
/// newest-first. Served from the DB system-of-record (RunRepository): the durable
/// per-repo PullRequestsJson projection (multi-repo complete), with pre-p0347 rows
/// falling back to the run's lone PR url so history isn't blank. Mapped only inside
/// Program.cs's <c>AGENTSMITH_UI_API_ENABLED</c> block, like the other read
/// endpoints, so a dashboard-less deployment never exposes it.
///
/// LIVE PR state (merged/open/abandoned via the git provider's PR API) is the
/// named follow-up p0347b — this serves what the agent RECORDED at open time.
/// </summary>
internal static class PullRequestQueryEndpoints
{
    // Match the runs list window: the recent history the dashboard shows.
    private const int RecentLimit = 200;

    internal static WebApplication MapPullRequestQueryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/pull-requests", GetPullRequestsAsync);
        return app;
    }

    private static async Task<IResult> GetPullRequestsAsync(RunRepository runs, CancellationToken cancellationToken) =>
        Results.Ok(await BuildListAsync(runs, cancellationToken));

    // Internal for the endpoint test: active (a PR can be opened mid-run on a
    // multi-repo run) + recent history, flattened newest-first. The two sets are
    // disjoint (FinishedAt null vs not-null) — no dedup needed.
    internal static async Task<List<PullRequestListItem>> BuildListAsync(
        RunRepository runs, CancellationToken cancellationToken)
    {
        var active = await runs.GetActiveRunsAsync(cancellationToken);
        var recent = await runs.GetRecentRunsAsync(RecentLimit, cancellationToken);
        return Flatten(active.Concat(recent));
    }

    internal static List<PullRequestListItem> Flatten(IEnumerable<Run> runs) =>
        runs.SelectMany(RowsFor)
            .OrderByDescending(x => x.OpenedAt)
            .ThenByDescending(x => x.RunId)
            .ToList();

    // p0347: a run's PR rows for the flattened list. Prefer the durable per-repo
    // PullRequestsJson (every repo the run opened); fall back to a SINGLE row from
    // the run's lone opened PR for pre-p0347 rows so history isn't blank. A run
    // that opened no PR contributes nothing.
    private static IEnumerable<PullRequestListItem> RowsFor(Run run)
    {
        var ticketId = string.IsNullOrEmpty(run.TicketId) ? null : run.TicketId;
        var stored = RunStoryJson.TryDeserialize<List<RunPullRequestView>>(run.PullRequestsJson);
        if (stored is { Count: > 0 })
        {
            foreach (var pr in stored)
                yield return new PullRequestListItem(
                    run.Id, ticketId, run.TicketTitle, run.Pipeline,
                    pr.Repo, pr.Status, pr.Url, pr.Reason, pr.OpenedAt);
            yield break;
        }

        var openedPr = run.Repos.FirstOrDefault(r => r.PrStatus == "opened");
        if (openedPr?.PrUrl is { } url)
            yield return new PullRequestListItem(
                run.Id, ticketId, run.TicketTitle, run.Pipeline,
                openedPr.RepoName, "opened", url, null, run.FinishedAt ?? run.StartedAt);
    }
}
