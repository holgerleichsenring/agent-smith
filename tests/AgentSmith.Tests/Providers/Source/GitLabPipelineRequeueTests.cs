using System.Net;
using System.Text;
using AgentSmith.Infrastructure.Services.Providers.Pipelines;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// 2026-11-25-7a92b: a GitLab merge request's settled head without a pipeline gets one through f9c5d's start (POST
/// merge_requests/:iid/pipelines, per docs.gitlab.com); a fresh head, a running one, a manual one and a fork's get nothing.
/// </summary>
public sealed class GitLabPipelineRequeueTests
{
    private const string Api = "https://gitlab.example.com/api/v4/projects/group%2Frepo";
    private const string Url = "https://gitlab.example.com/group/repo/-/merge_requests/7";
    private readonly List<string> _posts = [];

    [Fact]
    public async Task RequeueAsync_SettledHeadWithoutAPipeline_StartsOne()
    {
        (await RequeueAsync(Mr("""{"id":9,"status":"success","sha":"old"}"""), settled: true)).Should().Be(1);

        _posts.Should().Equal($"{Api}/merge_requests/7/pipelines");
    }

    [Theory]
    [InlineData("""{"id":9,"status":"success","sha":"old"}""", false)]
    [InlineData("""{"id":9,"status":"running","sha":"abc"}""", true)]
    [InlineData("""{"id":9,"status":"manual","sha":"abc"}""", true)]
    public async Task RequeueAsync_AFreshRunningOrManualHead_StartsNothing(string pipeline, bool settled)
    {
        (await RequeueAsync(Mr(pipeline), settled)).Should().Be(0);

        _posts.Should().BeEmpty();
    }

    [Fact]
    public async Task RequeueAsync_AForkMergeRequest_StartsNothing()
    {
        (await RequeueAsync(Mr("""{"id":9,"status":"success","sha":"old"}""", source: 99), settled: true)).Should().Be(0);

        _posts.Should().BeEmpty();
    }

    private static string Mr(string pipeline, int source = 1) =>
        $$"""{"iid":7,"sha":"abc","source_branch":"agent-smith/7","target_branch":"main","source_project_id":{{source}},"target_project_id":1,"detailed_merge_status":"ci_must_pass","head_pipeline":{{pipeline}}}""";

    private Task<int> RequeueAsync(string mr, bool settled)
    {
        var http = new HttpClient(new Handler(request =>
        {
            var url = request.RequestUri!.ToString();
            if (request.Method == HttpMethod.Post) { _posts.Add(url); return Json("""{"id":10,"web_url":"https://gl/p/10"}""", HttpStatusCode.Created); }
            return url switch
            {
                $"{Api}/merge_requests/7" => Json(mr),
                Api => Json("""{"default_branch":"main","ci_config_path":""}"""),
                _ when url.StartsWith($"{Api}/repository/files/", StringComparison.Ordinal) => Json("""{"file_name":".gitlab-ci.yml"}"""),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
        }));
        return new GitLabPipelineRequeue(new PipelineHttp(http, r => r.Headers.Add("PRIVATE-TOKEN", "glpat-test")), Api, NullLogger.Instance)
            .RequeueAsync(Url, settled, CancellationToken.None);
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(respond(request));
    }
}
