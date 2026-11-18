using System.Net;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Providers.Pipelines;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Pipelines;

/// <summary>2026-11-18-3863e: GitHub Actions — workflow_dispatch asking for the run it started, and a run read back with
/// its failed jobs, their failed steps and log end.</summary>
public sealed class GitHubPipelineRunnerTests
{
    private const string Api = "https://api.github.com/repos/example/sample-app";
    private readonly FakePipelineHost _host = new();

    [Fact]
    public async Task Start_PostsRefInputsAndReturnRunDetails_AndNamesTheRun()
    {
        _host.On("POST", "/actions/workflows/deploy.yml/dispatches",
            """{"workflow_run_id":4711,"run_url":"https://api.github.com/x","html_url":"https://github.com/example/sample-app/actions/runs/4711"}""");

        var start = await Runner("main").StartAsync(Order("refs/heads/main"), CancellationToken.None);

        start.Should().Be(new PipelineRunStart("4711", "https://github.com/example/sample-app/actions/runs/4711"));
        var (_, url, body) = _host.Requests.Single();
        url.Should().Be($"{Api}/actions/workflows/deploy.yml/dispatches");
        body.Should().Contain("\"ref\":\"main\"").And.Contain("\"version\":\"v2\"").And.Contain("\"return_run_details\":true");
    }

    [Fact]
    public async Task Start_NoRefAndNoDefaultBranch_ReadsTheRepositorysDefault_A204StartsUnnamed()
    {
        _host.On("GET", "/repos/example/sample-app", """{"default_branch":"trunk"}""")
            .On("POST", "/dispatches", "", HttpStatusCode.NoContent);

        var start = await Runner(null).StartAsync(Order(null), CancellationToken.None);

        start.RunId.Should().BeNull();
        start.Url.Should().Be("https://github.com/example/sample-app/actions/workflows/deploy.yml");
        _host.Requests[^1].Body.Should().Contain("\"ref\":\"trunk\"");
    }

    [Fact]
    public async Task Read_FailedRun_IsRedWithTheFailedJobStepsAndLogEnd()
    {
        _host.On("GET", "/actions/runs/4711", """{"status":"completed","conclusion":"failure","html_url":"https://gh/runs/4711"}""")
            .On("GET", "/actions/runs/4711/jobs?filter=latest&per_page=100",
                """{"jobs":[{"id":9,"name":"deploy","conclusion":"failure","html_url":"https://gh/jobs/9","steps":[{"name":"helm upgrade","conclusion":"failure"},{"name":"checkout","conclusion":"success"}]},{"id":10,"name":"lint","conclusion":"success"}]}""")
            .On("GET", "/actions/jobs/9/logs", "setup\nError: UPGRADE FAILED");

        var run = await Runner("main").ReadAsync("4711", new CiReadRequest(null, 4000, 5), CancellationToken.None);

        run.State.Should().Be(CiStates.Red);
        var failure = run.Failures.Single();
        failure.Name.Should().Be("deploy");
        failure.Url.Should().Be("https://gh/jobs/9");
        failure.Excerpt.Should().Contain("step failed: helm upgrade").And.EndWith("UPGRADE FAILED");
    }

    [Theory]
    [InlineData("in_progress", null, CiStates.Pending)]
    [InlineData("completed", "success", CiStates.Green)]
    public async Task Read_RunningOrSucceeded_IsPendingOrGreen(string status, string? conclusion, string expected)
    {
        _host.On("GET", "/actions/runs/4711", $$"""{"status":"{{status}}","conclusion":{{(conclusion is null ? "null" : $"\"{conclusion}\"")}}}""");

        (await Runner("main").ReadAsync("4711", new CiReadRequest(null, 4000, 5), CancellationToken.None)).State.Should().Be(expected);
    }

    [Fact]
    public async Task Start_WithoutAWorkflow_IsRefused() =>
        await FluentActions.Awaiting(() => Runner("main").StartAsync(new PipelineRunOrder(null, null, new Dictionary<string, string>()), default))
            .Should().ThrowAsync<ArgumentException>().WithMessage("*workflow by its file name*");

    private GitHubPipelineRunner Runner(string? defaultBranch) =>
        new(_host.Http(), GitHubRepoAddress.Parse("https://github.com/example/sample-app"), defaultBranch);

    private static PipelineRunOrder Order(string? reference) =>
        new("deploy.yml", reference, new Dictionary<string, string> { ["version"] = "v2" });
}
