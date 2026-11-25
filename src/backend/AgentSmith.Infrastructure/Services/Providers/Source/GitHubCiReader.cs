using System.Text.Json;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-17-3435a: a GitHub pull request's CI — the statusCheckRollup contexts of the commit at the read head, with
/// whether each is required for this pull request. A failed check run is read with its output and annotations, and one
/// GitHub Actions ran with the end of its job log, for at most the request's FailuresRead failures.
/// <para>2026-11-25-7a92a: on a settled head into a non-series branch, an expected status and a required context nobody
/// reported are not started once nothing else runs (<see cref="GitHubUnreportedChecks"/>); a series branch's silent head stays
/// none, which f9c5d's start serves.</para>
/// </summary>
public sealed class GitHubCiReader(
    IGitHubClient client, GitHubRepoAddress address, Func<long, CancellationToken, Task<string>> jobLog, ILogger? logger = null)
{
    private const string HeadQuery =
        "query($owner:String!,$repo:String!,$number:Int!){repository(owner:$owner,name:$repo){pullRequest(number:$number){headRefOid}}}";

    private const string ChecksQuery =
        "query($owner:String!,$repo:String!,$oid:GitObjectID!,$number:Int!){repository(owner:$owner,name:$repo){pullRequest(number:$number){baseRefName} object(oid:$oid){"
        + "... on Commit{statusCheckRollup{contexts(first:100){nodes{__typename "
        + "... on CheckRun{databaseId name status conclusion detailsUrl completedAt title summary text isRequired(pullRequestNumber:$number) "
        + "checkSuite{app{slug}} annotations(first:20){nodes{path message location{start{line}}}}} "
        + "... on StatusContext{context state targetUrl description createdAt isRequired(pullRequestNumber:$number)}}}}}}}}";

    public async Task<PullRequestCi> ReadAsync(string prUrl, CiReadRequest request, CancellationToken ct)
    {
        if (!GitHubPullRequestUpdater.TryParsePullNumber(prUrl, out var number))
            throw new ArgumentException($"Not a GitHub pull request URL: {prUrl}", nameof(prUrl));
        var query = new GitHubGraphQlQuery(client, address.Api.GraphQl);
        var head = request.HeadSha ?? (await query.RunAsync(HeadQuery, new { owner = address.Owner, repo = address.Repo, number },
            "pull-request head")).GetProperty("repository").GetProperty("pullRequest").GetProperty("headRefOid").GetString();
        if (head is null) return PullRequestCi.NoneOn(null);
        var data = await query.RunAsync(ChecksQuery, new { owner = address.Owner, repo = address.Repo, oid = head, number }, "CI checks");
        var nodes = Nodes(data);
        var checks = await ChecksAsync(nodes, BaseOf(data), request.HeadSettled, ct);
        var state = CiVerdict.Of(checks);
        return new PullRequestCi(state, head, CiVerdict.At(checks),
            state == CiStates.Red ? await FailuresAsync(nodes, checks, request, ct) : []);
    }

    private async Task<IReadOnlyList<CiCheck>> ChecksAsync(IReadOnlyList<JsonElement> nodes, string? into, bool settled, CancellationToken ct)
    {
        var reported = nodes.Select(GitHubCiContexts.Check).ToList();
        var expected = nodes.Select(GitHubCiContexts.IsExpected).ToList();
        if (into is null || AgentSmith.Contracts.Providers.WorkBranches.IsSeries(into) || !GitHubUnreportedChecks.Judged(reported, expected, settled))
            return reported;
        var required = await new GitHubRequiredContexts(client, address, logger ?? NullLogger.Instance).ReadAsync(into, ct);
        return GitHubUnreportedChecks.Settle(reported, expected, required);
    }

    private static string? BaseOf(JsonElement data) =>
        data.GetProperty("repository").TryGetProperty("pullRequest", out var pr) && pr.ValueKind == JsonValueKind.Object
        && pr.TryGetProperty("baseRefName", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString() : null;

    private async Task<IReadOnlyList<CiFailure>> FailuresAsync(
        IReadOnlyList<JsonElement> nodes, IReadOnlyList<CiCheck> checks, CiReadRequest request, CancellationToken ct)
    {
        var deciding = CiVerdict.Deciding(checks);
        // Settled checks keep the nodes' order; the absent required ones are appended after them and have no node.
        var failed = nodes.Where((_, i) => checks[i].Outcome == CiCheckOutcome.Failed && deciding.Contains(checks[i])).ToList();
        var failures = new List<CiFailure>();
        foreach (var node in failed)
        {
            var log = failures.Count < request.FailuresRead && GitHubCiContexts.ActionsJob(node) is { } job ? await LogAsync(job, ct) : null;
            failures.Add(new CiFailure(GitHubCiContexts.Check(node).Name, GitHubCiContexts.Link(node),
                CiExcerpt.Of(GitHubCiContexts.Summary(node), log, request.ExcerptChars)));
        }
        return failures;
    }

    // A log that cannot be read (expired, no Actions scope) costs the tail, not the evidence.
    private async Task<string?> LogAsync(long job, CancellationToken ct)
    {
        try { return await jobLog(job, ct); }
        catch (Exception ex) when (ex is ApiException or HttpRequestException) { return $"(log not readable: {ex.Message})"; }
    }

    private static IReadOnlyList<JsonElement> Nodes(JsonElement data) =>
        data.GetProperty("repository").TryGetProperty("object", out var commit) && commit.ValueKind == JsonValueKind.Object
        && commit.TryGetProperty("statusCheckRollup", out var rollup) && rollup.ValueKind == JsonValueKind.Object
            ? [.. rollup.GetProperty("contexts").GetProperty("nodes").EnumerateArray()]
            : [];
}
