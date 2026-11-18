using System.Net;
using System.Text;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// 2026-11-17-3435a: a GitLab merge request's CI from its head pipeline; a failed one is read with its failed jobs,
/// allowed failures left out — stage, name, failure reason and the end of each job's trace, linked to the job.
/// </summary>
public sealed class GitLabCiReaderTests
{
    private const string MrUrl = "https://gitlab.example.com/group/repo/-/merge_requests/7";

    [Theory]
    [InlineData("success", CiStates.Green)]
    [InlineData("running", CiStates.Pending)]
    [InlineData("manual", CiStates.Pending)]
    public async Task HeadPipelineStatus_DecidesTheHead(string status, string expected) =>
        (await ReadAsync(Mr($$"""{"id":9,"status":"{{status}}","sha":"abc","web_url":"https://gl/p/9"}"""))).State.Should().Be(expected);

    [Fact]
    public async Task NoPipeline_IsNone() => (await ReadAsync(Mr("null"))).State.Should().Be(CiStates.None);

    [Fact]
    public async Task PipelineOfAnOlderHead_IsPending() =>
        (await ReadAsync(Mr("""{"id":9,"status":"failed","sha":"old","web_url":"https://gl/p/9"}"""))).State.Should().Be(CiStates.Pending);

    [Fact]
    public async Task Failed_IsRed_WithTheFailedJobsStageNameTraceAndLink()
    {
        var ci = await ReadAsync(Mr("""{"id":9,"status":"failed","sha":"abc","web_url":"https://gl/p/9"}"""));

        ci.State.Should().Be(CiStates.Red);
        var failure = ci.Failures.Single();
        failure.Name.Should().Be("test: unit");
        failure.Url.Should().Be("https://gl/j/31");
        failure.Excerpt.Should().Contain("failure reason: script_failure").And.EndWith("FAILED test_add");
    }

    private static string Mr(string pipeline) => $$"""{"iid":7,"sha":"abc","head_pipeline":{{pipeline}}}""";

    private static Task<PullRequestCi> ReadAsync(string mr)
    {
        var http = new HttpClient(new Handler(url => url switch
        {
            _ when url.EndsWith("/merge_requests/7", StringComparison.Ordinal) => Json(mr),
            _ when url.Contains("/pipelines/9/jobs", StringComparison.Ordinal) =>
                Json("""[{"id":31,"name":"unit","stage":"test","web_url":"https://gl/j/31","failure_reason":"script_failure"},{"id":32,"name":"flaky","stage":"test","allow_failure":true}]"""),
            _ when url.EndsWith("/jobs/31/trace", StringComparison.Ordinal) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("setup\nFAILED test_add") },
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        }));
        return new GitLabCiReader("https://gitlab.example.com", "group%2Frepo", "glpat-test", http)
            .ReadAsync(MrUrl, new CiReadRequest(null, 4000, 5), CancellationToken.None);
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(respond(request.RequestUri!.ToString()));
    }
}
