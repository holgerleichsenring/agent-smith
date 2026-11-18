using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// 2026-11-17-3435a: a GitHub pull request's CI from the statusCheckRollup of its head — check runs and commit statuses.
/// A failed check run is read with its output and annotations, one GitHub Actions ran with the end of its job log.
/// </summary>
public sealed class GitHubCiReaderTests
{
    private const string PrUrl = "https://github.com/example/sample-server/pull/7";
    private readonly List<long> _logsRead = [];

    [Fact]
    public async Task AllChecksPassed_IsGreen()
    {
        var ci = await ReadAsync(Run("build", "COMPLETED", "SUCCESS"), Status("lint", "SUCCESS"));

        ci.State.Should().Be(CiStates.Green);
        ci.HeadSha.Should().Be("abc");
    }

    [Fact]
    public async Task OneInProgress_IsPending() =>
        (await ReadAsync(Run("build", "COMPLETED", "SUCCESS"), Run("test", "IN_PROGRESS", null))).State.Should().Be(CiStates.Pending);

    [Fact]
    public async Task NoContexts_IsNone() => (await ReadAsync()).State.Should().Be(CiStates.None);

    [Fact]
    public async Task FailedActionsCheck_IsRed_WithAnnotationsAndTheLogTail()
    {
        var ci = await ReadAsync(200, Run("test", "COMPLETED", "FAILURE", actions: true));

        ci.State.Should().Be(CiStates.Red);
        var failure = ci.Failures.Single();
        failure.Name.Should().Be("test");
        failure.Url.Should().Be("https://ci.example/test");
        failure.Excerpt.Should().Contain("src/Calc.cs:12 Expected 3 but was 4").And.EndWith("Tests failed: 1");
        failure.Excerpt.Length.Should().BeLessThanOrEqualTo(200);
        _logsRead.Should().Equal(42L);
    }

    [Fact]
    public async Task FailedCommitStatus_IsRed_WithItsTargetUrl()
    {
        var ci = await ReadAsync(Status("external-ci", "FAILURE"));

        ci.State.Should().Be(CiStates.Red);
        ci.Failures.Single().Url.Should().Be("https://ci.example/external-ci");
        ci.Failures.Single().Excerpt.Should().Be("external-ci said FAILURE");
        _logsRead.Should().BeEmpty("a commit status has no Actions job");
    }

    private Task<PullRequestCi> ReadAsync(params string[] nodes) => ReadAsync(4000, nodes);

    private Task<PullRequestCi> ReadAsync(int chars, params string[] nodes)
    {
        var reply = """{"data":{"repository":{"object":{"statusCheckRollup":{"contexts":{"nodes":[""" + string.Join(",", nodes) + "]}}}}}}";
        var client = GitHubGraphQlHost.Replying(nodes.Length == 0 ? """{"data":{"repository":{"object":{"statusCheckRollup":null}}}}""" : reply);
        var reader = new GitHubCiReader(client, GitHubRepoAddress.Parse("https://github.com/example/sample-server"), (job, _) =>
        {
            _logsRead.Add(job);
            return Task.FromResult(new string('.', 1000) + "\nTests failed: 1");
        });
        return reader.ReadAsync(PrUrl, new CiReadRequest("abc", chars, 5), CancellationToken.None);
    }

    private static string Run(string name, string status, string? conclusion, bool actions = false) =>
        "{\"__typename\":\"CheckRun\",\"databaseId\":42,\"name\":\"" + name + "\",\"status\":\"" + status + "\",\"conclusion\":"
        + (conclusion is null ? "null" : $"\"{conclusion}\"") + ",\"detailsUrl\":\"https://ci.example/" + name
        + "\",\"completedAt\":\"2026-11-17T10:00:00Z\",\"title\":\"" + name + " failed\",\"summary\":\"1 test failed\",\"text\":null,"
        + "\"isRequired\":false,\"checkSuite\":{\"app\":{\"slug\":\"" + (actions ? "github-actions" : "other") + "\"}},"
        + "\"annotations\":{\"nodes\":[{\"path\":\"src/Calc.cs\",\"message\":\"Expected 3 but was 4\",\"location\":{\"start\":{\"line\":12}}}]}}";

    private static string Status(string context, string state) =>
        $$"""{"__typename":"StatusContext","context":"{{context}}","state":"{{state}}","targetUrl":"https://ci.example/{{context}}","description":"{{context}} said {{state}}","createdAt":"2026-11-17T10:00:00Z","isRequired":false}""";
}
