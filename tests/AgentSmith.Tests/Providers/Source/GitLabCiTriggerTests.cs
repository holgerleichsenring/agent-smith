using System.Net;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Tests.Providers.Pipelines;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>2026-11-24-f9c5d: GitLab — a merge-request pipeline, a branch pipeline when that is refused, GitLab's words when
/// both are, and no CI when the project's CI file is missing on the default branch.</summary>
public sealed class GitLabCiTriggerTests
{
    private const string Api = "https://gitlab.example.com/api/v4/projects/group%2Fapp";
    private const string MrUrl = "https://gitlab.example.com/group/app/-/merge_requests/3";
    private readonly FakePipelineHost _host = new();

    public GitLabCiTriggerTests() => _host.On("GET", "/projects/group%2Fapp", """{"default_branch":"main","ci_config_path":""}""");

    [Fact]
    public async Task AMergeRequestPipeline_IsStartedWithItsLink()
    {
        _host.On("GET", "/repository/files/.gitlab-ci.yml?ref=main", "{}")
            .On("POST", "/merge_requests/3/pipelines", """{"id":9,"web_url":"https://gitlab.example.com/group/app/-/pipelines/9"}""", HttpStatusCode.Created);

        var answer = await StartAsync();

        answer.Should().Be(CiTrigger.Started("created a merge-request pipeline", "https://gitlab.example.com/group/app/-/pipelines/9"));
    }

    [Fact]
    public async Task ARefusedMergeRequestPipeline_FallsBackToTheSourceBranch()
    {
        _host.On("GET", "/repository/files/.gitlab-ci.yml?ref=main", "{}")
            .On("POST", "/merge_requests/3/pipelines", """{"message":"No stages / jobs for this pipeline."}""", HttpStatusCode.BadRequest)
            .On("GET", "/merge_requests/3", """{"source_branch":"agent-smith/42"}""")
            .On("POST", "/pipeline", """{"id":10,"web_url":"https://x/pipelines/10"}""", HttpStatusCode.Created);

        var answer = await StartAsync();

        answer.Outcome.Should().Be(CiTriggerOutcome.Started);
        answer.Text.Should().Contain("agent-smith/42");
        _host.Requests[^1].Body.Should().Contain("\"ref\":\"agent-smith/42\"");
    }

    [Fact]
    public async Task BothRefused_IsImpossibleInGitLabsWords()
    {
        _host.On("GET", "/repository/files/.gitlab-ci.yml?ref=main", "{}")
            .On("POST", "/merge_requests/3/pipelines", """{"message":"rules"}""", HttpStatusCode.BadRequest)
            .On("GET", "/merge_requests/3", """{"source_branch":"agent-smith/42"}""")
            .On("POST", "/pipeline", """{"message":"Reference not found"}""", HttpStatusCode.BadRequest);

        var answer = await StartAsync();

        answer.Outcome.Should().Be(CiTriggerOutcome.Impossible);
        answer.Text.Should().Contain("Reference not found");
    }

    [Fact]
    public async Task NoCiFileOnTheDefaultBranch_IsNoCi() =>
        (await StartAsync()).Outcome.Should().Be(CiTriggerOutcome.NoCi);

    private Task<CiTrigger> StartAsync() => new GitLabCiTrigger(_host.Http(), Api).StartAsync(MrUrl, null, default);
}
