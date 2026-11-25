using System.Net;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// 2026-11-25-7a92a: a GitHub head whose CI will not start by itself reads not_started — a STALE check run at once (GitHub gave
/// it up after 14 days), and on a settled head with nothing running a status GitHub EXPECTS or a required context nobody
/// reported (rulesets, then classic protection; an unreadable source adds nothing). Payloads follow docs.github.com: the
/// statusCheckRollup contexts, GET /repos/{o}/{r}/rules/branches/{b}, GET .../branches/{b}/protection/required_status_checks.
/// </summary>
public sealed class GitHubCiNotStartedTests
{
    private const string Rules = "GET repos/example/sample-server/rules/branches/main";
    private const string Classic = "GET repos/example/sample-server/branches/main/protection/required_status_checks";
    private readonly GitHubGraphQlHost _host = new();

    [Fact]
    public async Task ReadAsync_StaleCheckRun_IsNotStartedOnAnyHead() =>
        (await ReadAsync(settled: false, Run("build", "COMPLETED", "STALE"))).State.Should().Be(CiStates.NotStarted);

    [Theory]
    [InlineData("SKIPPED")]
    [InlineData("NEUTRAL")]
    public async Task ReadAsync_SkippedOrNeutral_IsGreen(string conclusion) =>
        (await ReadAsync(settled: true, Run("build", "COMPLETED", conclusion))).State.Should().Be(CiStates.Green);

    [Fact]
    public async Task ReadAsync_RequiredContextNobodyReported_OnASettledHead_IsNotStarted()
    {
        _host.Rest = call => call == Rules ? (HttpStatusCode.OK, """[{"type":"required_status_checks","parameters":{"strict_required_status_checks_policy":false,"required_status_checks":[{"context":"deploy-check","integration_id":15368}]}}]""") : null;

        var ci = await ReadAsync(settled: true, Run("build", "COMPLETED", "SUCCESS"));

        ci.State.Should().Be(CiStates.NotStarted, "the required deploy-check never reported and nothing else runs");
    }

    [Fact]
    public async Task ReadAsync_ClassicProtectionContext_CountsWhenTheRulesetsRequireNothing()
    {
        _host.Rest = call => call == Classic ? (HttpStatusCode.OK, """{"strict":true,"contexts":["ci/legacy"],"checks":[{"context":"ci/legacy","app_id":null}]}""") : null;

        (await ReadAsync(settled: true, Run("build", "COMPLETED", "SUCCESS"))).State.Should().Be(CiStates.NotStarted);
    }

    [Fact]
    public async Task ReadAsync_RequiredContextNobodyReported_WhileAnotherRuns_IsPending()
    {
        _host.Rest = call => call == Rules ? (HttpStatusCode.OK, """[{"type":"required_status_checks","parameters":{"required_status_checks":[{"context":"deploy-check"}]}}]""") : null;

        (await ReadAsync(settled: true, Run("build", "IN_PROGRESS", null))).State.Should().Be(CiStates.Pending);
        _host.Requests.Should().NotContain(r => r.Query.StartsWith("GET", StringComparison.Ordinal), "nothing is judged while a check runs");
    }

    [Fact]
    public async Task ReadAsync_UnsettledHead_JudgesNothingUnreported()
    {
        _host.Rest = call => call == Rules ? (HttpStatusCode.OK, """[{"type":"required_status_checks","parameters":{"required_status_checks":[{"context":"deploy-check"}]}}]""") : null;

        (await ReadAsync(settled: false, Run("build", "COMPLETED", "SUCCESS"), Status("legal", "EXPECTED"))).State.Should().Be(CiStates.Pending);
    }

    [Fact]
    public async Task ReadAsync_ExpectedStatus_OnASettledHead_IsNotStarted() =>
        (await ReadAsync(settled: true, Run("build", "COMPLETED", "SUCCESS"), Status("legal", "EXPECTED"))).State.Should().Be(CiStates.NotStarted);

    [Fact]
    public async Task ReadAsync_ProtectionUnreadable_AddsNoRequiredCheck()
    {
        _host.Rest = _ => (HttpStatusCode.Forbidden, """{"message":"Resource not accessible by integration"}""");

        (await ReadAsync(settled: true, Run("build", "COMPLETED", "SUCCESS"))).State.Should().Be(CiStates.Green);
    }

    [Fact]
    public async Task ReadAsync_SeriesBranchTarget_KeepsItsSilentHeadForTheSeriesStart()
    {
        _host.Rest = _ => (HttpStatusCode.OK, """[{"type":"required_status_checks","parameters":{"required_status_checks":[{"context":"deploy-check"}]}}]""");

        (await ReadIntoAsync(true, "agent-smith/series/7a92")).State.Should().Be(CiStates.None);
    }

    [Fact]
    public async Task ReadAsync_NothingReportedAndARequiredContext_OnASettledHead_IsNotStartedNotNone()
    {
        _host.Rest = call => call == Rules ? (HttpStatusCode.OK, """[{"type":"required_status_checks","parameters":{"required_status_checks":[{"context":"build"}]}}]""") : null;

        (await ReadAsync(settled: true)).State.Should().Be(CiStates.NotStarted, "a required check that never reported is no 'no CI'");
    }

    [Fact]
    public async Task ReadAsync_ARedHead_StaysRedWhateverNeverReported()
    {
        _host.Rest = _ => (HttpStatusCode.OK, """[{"type":"required_status_checks","parameters":{"required_status_checks":[{"context":"deploy-check"}]}}]""");

        (await ReadAsync(settled: true, Run("lint", "COMPLETED", "FAILURE"))).State.Should().Be(CiStates.Red);
        _host.Requests.Should().NotContain(r => r.Query.StartsWith("GET", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReadAsync_ABaseWithASlash_IsAskedEscaped()
    {
        _host.Rest = call => call == "GET repos/example/sample-server/rules/branches/release%2F1.0"
            ? (HttpStatusCode.OK, """[{"type":"required_status_checks","parameters":{"required_status_checks":[{"context":"build"}]}}]""") : null;

        (await ReadIntoAsync(true, "release/1.0")).State.Should().Be(CiStates.NotStarted);
    }

    [Fact]
    public async Task ReadAsync_StaleOnASeriesBranch_IsStillNotStarted() =>
        (await ReadIntoAsync(true, "agent-smith/series/7a92", Run("build", "COMPLETED", "STALE"))).State.Should().Be(CiStates.NotStarted);

    private Task<PullRequestCi> ReadAsync(bool settled, params string[] nodes) => ReadIntoAsync(settled, "main", nodes);

    private Task<PullRequestCi> ReadIntoAsync(bool settled, string into, params string[] nodes)
    {
        var reply = """{"data":{"repository":{"pullRequest":{"baseRefName":"BASE"},"object":{"statusCheckRollup":{"contexts":{"nodes":["""
            .Replace("BASE", into, StringComparison.Ordinal) + string.Join(",", nodes) + "]}}}}}}";
        var client = _host.Client((_, _) => reply);
        return new GitHubCiReader(client, GitHubRepoAddress.Parse("https://github.com/example/sample-server"), (_, _) => Task.FromResult(string.Empty))
            .ReadAsync("https://github.com/example/sample-server/pull/7", new CiReadRequest("abc", 4000, 5) { HeadSettled = settled }, CancellationToken.None);
    }

    private static string Run(string name, string status, string? conclusion) =>
        "{\"__typename\":\"CheckRun\",\"databaseId\":42,\"name\":\"" + name + "\",\"status\":\"" + status + "\",\"conclusion\":"
        + (conclusion is null ? "null" : $"\"{conclusion}\"") + ",\"detailsUrl\":null,\"completedAt\":null,\"isRequired\":false,"
        + "\"checkSuite\":{\"app\":{\"slug\":\"github-actions\"}},\"annotations\":{\"nodes\":[]}}";

    private static string Status(string context, string state) =>
        $$"""{"__typename":"StatusContext","context":"{{context}}","state":"{{state}}","targetUrl":null,"description":null,"createdAt":"2026-11-25T10:00:00Z","isRequired":false}""";
}
