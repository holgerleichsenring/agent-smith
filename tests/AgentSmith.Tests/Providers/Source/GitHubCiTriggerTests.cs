using System.Net;
using System.Text;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Tests.Providers.Pipelines;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>2026-11-24-f9c5d: GitHub — the workflows that run for pull requests into the default branch, read from their
/// files; GitHub's own start when one already takes the series branch, a dispatch on the head when declared, the fix named
/// when not, and no CI when none runs for pull requests.</summary>
public sealed class GitHubCiTriggerTests
{
    private const string PrUrl = "https://github.com/example/sample-app/pull/5";
    private readonly FakePipelineHost _host = new();

    public GitHubCiTriggerTests() =>
        _host.On("GET", "/pulls/5", """{"head":{"ref":"agent-smith/42"},"base":{"ref":"agent-smith/series/sr-1"}}""")
            .On("GET", "/actions/workflows?per_page=100",
                """{"workflows":[{"id":11,"path":".github/workflows/ci.yml","state":"active"},{"id":12,"path":"dynamic/pages","state":"active"}]}""");

    [Fact]
    public async Task APullRequestWorkflowOnMain_WithDispatch_IsDispatchedOnTheHead()
    {
        Workflow("on:\n  pull_request:\n    branches: [main]\n  workflow_dispatch:\n");
        _host.On("POST", "/actions/workflows/11/dispatches", "", HttpStatusCode.NoContent);

        var answer = await StartAsync();

        answer.Outcome.Should().Be(CiTriggerOutcome.Started);
        answer.Text.Should().Contain("dispatched ci.yml on agent-smith/42");
        _host.Requests.Single(r => r.Method == "POST").Body.Should().Contain("\"ref\":\"agent-smith/42\"");
    }

    [Fact]
    public async Task WithoutDispatch_IsImpossible_NamingTheFileAndTheFixes()
    {
        Workflow("on:\n  pull_request:\n    branches: [main]\n");

        var answer = await StartAsync();

        answer.Outcome.Should().Be(CiTriggerOutcome.Impossible);
        answer.Text.Should().Contain("ci.yml").And.Contain("agent-smith/series/**").And.Contain("workflow_dispatch");
    }

    [Fact]
    public async Task AFilterThatTakesTheSeriesBranch_IsGitHubsOwnStart_AndDispatchesNothing()
    {
        Workflow("on:\n  pull_request:\n    branches: [main, 'agent-smith/**']\n  workflow_dispatch:\n");

        var answer = await StartAsync();

        answer.Outcome.Should().Be(CiTriggerOutcome.Started);
        _host.Requests.Should().NotContain(r => r.Method == "POST");
    }

    [Fact]
    public async Task NoWorkflowForPullRequests_IsNoCi()
    {
        Workflow("on: push\n");

        (await StartAsync()).Outcome.Should().Be(CiTriggerOutcome.NoCi);
    }

    [Fact]
    public async Task ADispatchTheTokenMayNotMake_NamesActionsWrite()
    {
        Workflow("on:\n  pull_request:\n    branches: [main]\n  workflow_dispatch:\n");
        _host.On("POST", "/actions/workflows/11/dispatches", """{"message":"Resource not accessible"}""", HttpStatusCode.Forbidden);

        var answer = await StartAsync();

        answer.Outcome.Should().Be(CiTriggerOutcome.Impossible);
        answer.Text.Should().Contain("Actions: write");
    }

    private void Workflow(string yaml) =>
        _host.On("GET", "/contents/.github/workflows/ci.yml?ref=main",
            $$"""{"content":"{{Convert.ToBase64String(Encoding.UTF8.GetBytes(yaml))}}"}""");

    private Task<CiTrigger> StartAsync() =>
        new GitHubCiTrigger(_host.Http(), GitHubRepoAddress.Parse("https://github.com/example/sample-app")).StartAsync(PrUrl, "main", default);
}
