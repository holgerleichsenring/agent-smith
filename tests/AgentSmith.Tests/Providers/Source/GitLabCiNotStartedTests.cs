using System.Net;
using System.Text;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// 2026-11-25-7a92a: a GitLab merge request whose head has no pipeline reads not_started only on a settled head of the merge
/// request into a non-series branch — when an older head had one, or GitLab says ci_must_pass — and waits or has none otherwise.
/// Payloads follow docs.gitlab.com's single merge request (sha, head_pipeline, detailed_merge_status, target_branch).
/// </summary>
public sealed class GitLabCiNotStartedTests
{
    private const string Older = """{"id":9,"status":"success","sha":"old","web_url":"https://gl/p/9"}""";

    [Theory]
    [InlineData(true, CiStates.NotStarted)]
    [InlineData(false, CiStates.Pending)]
    public async Task ReadAsync_PipelineOfAnOlderHead_IsNotStartedOnlyOnceSettled(bool settled, string expected) =>
        (await ReadAsync(Mr(Older), settled)).State.Should().Be(expected);

    [Theory]
    [InlineData("ci_must_pass", true, CiStates.NotStarted)]
    [InlineData("ci_must_pass", false, CiStates.None)]
    [InlineData("mergeable", true, CiStates.None)]
    public async Task ReadAsync_NoPipelineEver_IsNoneUnlessGitLabSaysOneMustPass(string status, bool settled, string expected) =>
        (await ReadAsync(Mr("null", status), settled)).State.Should().Be(expected);

    [Fact]
    public async Task ReadAsync_SeriesBranchTarget_KeepsItsSilentHeadForTheSeriesStart() =>
        (await ReadAsync(Mr("null", "ci_must_pass", "agent-smith/series/7a92"), settled: true)).State.Should().Be(CiStates.None);

    [Fact]
    public async Task ReadAsync_AHeadTheMergeRequestNoLongerHas_StaysPending() =>
        (await ReadAsync(Mr(Older), settled: true, head: "gone")).State.Should().Be(CiStates.Pending);

    private static string Mr(string pipeline, string status = "ci_must_pass", string target = "main") =>
        $$"""{"iid":7,"sha":"abc","target_branch":"{{target}}","detailed_merge_status":"{{status}}","head_pipeline":{{pipeline}}}""";

    private static Task<PullRequestCi> ReadAsync(string mr, bool settled, string? head = null)
    {
        var http = new HttpClient(new Handler(url => url.EndsWith("/merge_requests/7", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(mr, Encoding.UTF8, "application/json") }
            : new HttpResponseMessage(HttpStatusCode.NotFound)));
        return new GitLabCiReader("https://gitlab.example.com", "group%2Frepo", "glpat-test", http).ReadAsync(
            "https://gitlab.example.com/group/repo/-/merge_requests/7", new CiReadRequest(head, 4000, 5) { HeadSettled = settled }, CancellationToken.None);
    }

    private sealed class Handler(Func<string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(respond(request.RequestUri!.ToString()));
    }
}
