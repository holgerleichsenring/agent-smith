using System.Text.Json;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Models;
using Microsoft.Extensions.Logging;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-25-7a92b: queues a GitHub pull request's not-started checks again where a run exists to re-run — the workflow run behind
/// each deciding STALE check GitHub Actions ran is re-run once (POST actions/runs/{id}/rerun, Actions write). A stale check of
/// another app, and a required check that never reported, have no run agent-smith can re-run; they are logged. A pull request
/// from another repository is never re-run: its runs wait on a maintainer's approval, which a re-run would pass.
/// </summary>
public sealed class GitHubStaleRunRequeue(IGitHubClient client, GitHubRepoAddress address, ILogger logger)
{
    private const string Query =
        "query($owner:String!,$repo:String!,$number:Int!){repository(owner:$owner,name:$repo){pullRequest(number:$number){isCrossRepository "
        + "commits(last:1){nodes{commit{statusCheckRollup{contexts(first:100){nodes{__typename "
        + "... on CheckRun{name status conclusion isRequired(pullRequestNumber:$number) checkSuite{app{slug} workflowRun{databaseId}}} "
        + "... on StatusContext{context state isRequired(pullRequestNumber:$number)}}}}}}}}}}";

    public async Task<int> RequeueAsync(string prUrl, CancellationToken ct)
    {
        if (!GitHubPullRequestUpdater.TryParsePullNumber(prUrl, out var number))
            throw new ArgumentException($"Not a GitHub pull request URL: {prUrl}", nameof(prUrl));
        var pr = (await new GitHubGraphQlQuery(client, address.Api.GraphQl).RunAsync(Query,
            new { owner = address.Owner, repo = address.Repo, number }, "stale checks")).GetProperty("repository").GetProperty("pullRequest");
        if (pr.GetProperty("isCrossRepository").GetBoolean())
        {
            logger.LogInformation("PR #{Pr} comes from another repository; its runs are not re-run by agent-smith", number);
            return 0;
        }
        var nodes = Nodes(pr);
        var checks = nodes.Select(GitHubCiContexts.Check).ToList();
        var deciding = CiVerdict.Deciding(checks);
        var runs = new SortedSet<long>();
        for (var i = 0; i < nodes.Count; i++)
            if (checks[i].Outcome == CiCheckOutcome.NotStarted && deciding.Contains(checks[i]))
                if (Run(nodes[i]) is { } run) runs.Add(run);
                else logger.LogInformation("PR #{Pr}: '{Check}' is stale and no GitHub Actions run of it can be re-run", number, checks[i].Name);
        var rerun = 0;
        foreach (var run in runs)
            if (await RerunAsync(number, run, ct)) rerun++;
        return rerun;
    }

    // One run GitHub refuses (a run older than GitHub re-runs, missing Actions write) leaves the others to be re-run.
    private async Task<bool> RerunAsync(int number, long run, CancellationToken ct)
    {
        try
        {
            await client.Connection.Post(new Uri($"repos/{address.Owner}/{address.Repo}/actions/runs/{run}/rerun", UriKind.Relative), ct);
            logger.LogInformation("PR #{Pr}: workflow run {Run} re-run — its checks were stale", number, run);
            return true;
        }
        catch (ApiException ex)
        {
            logger.LogWarning(ex, "PR #{Pr}: GitHub refused to re-run workflow run {Run} ({Status})", number, run, ex.StatusCode);
            return false;
        }
    }

    private static IReadOnlyList<JsonElement> Nodes(JsonElement pr) =>
        pr.GetProperty("commits").GetProperty("nodes").EnumerateArray().FirstOrDefault() is { ValueKind: JsonValueKind.Object } head
        && head.GetProperty("commit").TryGetProperty("statusCheckRollup", out var rollup) && rollup.ValueKind == JsonValueKind.Object
            ? [.. rollup.GetProperty("contexts").GetProperty("nodes").EnumerateArray()] : [];

    // The workflow run behind a check run GitHub Actions ran; null for another app's or a status.
    private static long? Run(JsonElement node) =>
        node.TryGetProperty("checkSuite", out var suite) && suite.ValueKind == JsonValueKind.Object
        && suite.TryGetProperty("app", out var app) && app.ValueKind == JsonValueKind.Object
        && app.TryGetProperty("slug", out var slug) && slug.GetString() == "github-actions"
        && suite.TryGetProperty("workflowRun", out var run) && run.ValueKind == JsonValueKind.Object
        && run.TryGetProperty("databaseId", out var id) && id.ValueKind == JsonValueKind.Number ? id.GetInt64() : null;
}
