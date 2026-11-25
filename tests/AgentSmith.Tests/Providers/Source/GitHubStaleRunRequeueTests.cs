using System.Net;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// 2026-11-25-7a92b: a GitHub pull request's deciding STALE checks that GitHub Actions ran have their workflow run re-run once
/// (POST /repos/{o}/{r}/actions/runs/{id}/rerun, per docs.github.com); another app's stale check, a passed one and a fork's get nothing.
/// </summary>
public sealed class GitHubStaleRunRequeueTests
{
    private readonly GitHubGraphQlHost _host = new() { Rest = _ => (HttpStatusCode.Created, "") };

    [Fact]
    public async Task RequeueAsync_TwoStaleChecksOfOneRun_ReRunItOnce()
    {
        (await RequeueAsync(false, Run("build", "STALE", 77), Run("test", "STALE", 77), Run("lint", "SUCCESS", 78))).Should().Be(1);

        Posts().Should().Equal("POST repos/example/sample-server/actions/runs/77/rerun");
    }

    [Fact]
    public async Task RequeueAsync_AnotherAppsStaleCheck_HasNoRunToReRun()
    {
        (await RequeueAsync(false, Run("external", "STALE", null))).Should().Be(0);

        Posts().Should().BeEmpty();
    }

    [Fact]
    public async Task RequeueAsync_AForkPullRequest_IsNeverReRun()
    {
        (await RequeueAsync(true, Run("build", "STALE", 77))).Should().Be(0);

        Posts().Should().BeEmpty();
    }

    [Fact]
    public async Task RequeueAsync_ARequiredCheckThatNeverReported_HasNoRunToReRun()
    {
        (await RequeueAsync(false, Run("lint", "SUCCESS", 78))).Should().Be(0, "an absent check has no node and no run");

        Posts().Should().BeEmpty();
    }

    [Fact]
    public async Task RequeueAsync_ARefusedRerun_LeavesTheOthersReRun()
    {
        _host.Rest = call => call.Contains("/runs/77/", StringComparison.Ordinal) ? (HttpStatusCode.Forbidden, """{"message":"too old"}""") : (HttpStatusCode.Created, "");

        (await RequeueAsync(false, Run("build", "STALE", 77), Run("test", "STALE", 79))).Should().Be(1);
    }

    private IEnumerable<string> Posts() => _host.Requests.Select(r => r.Query).Where(q => q.StartsWith("POST", StringComparison.Ordinal));

    private Task<int> RequeueAsync(bool fork, params string[] nodes)
    {
        var reply = """{"data":{"repository":{"pullRequest":{"isCrossRepository":FORK,"commits":{"nodes":[{"commit":{"statusCheckRollup":{"contexts":{"nodes":["""
            .Replace("FORK", fork ? "true" : "false", StringComparison.Ordinal) + string.Join(",", nodes) + "]}}}}]}}}}}";
        return new GitHubStaleRunRequeue(_host.Client((_, _) => reply), GitHubRepoAddress.Parse("https://github.com/example/sample-server"),
            NullLogger.Instance).RequeueAsync("https://github.com/example/sample-server/pull/7", CancellationToken.None);
    }

    private static string Run(string name, string conclusion, long? run) =>
        "{\"__typename\":\"CheckRun\",\"name\":\"" + name + "\",\"status\":\"COMPLETED\",\"conclusion\":\"" + conclusion + "\",\"isRequired\":false,"
        + (run is { } id
            ? "\"checkSuite\":{\"app\":{\"slug\":\"github-actions\"},\"workflowRun\":{\"databaseId\":" + id + "}}}"
            : "\"checkSuite\":{\"app\":{\"slug\":\"other-ci\"},\"workflowRun\":null}}");
}
