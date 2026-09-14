using System.Net;
using System.Text;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// 2026-09-13-a284: the provider opens against the base it is TOLD, and moves one that is
/// already open. GitLab stands for all three remote hosts here — it is the one whose wire
/// call is a plain HTTP body a test can read, so what reaches the platform is asserted
/// rather than assumed.
/// </summary>
public sealed class GitLabSourceProviderTargetTests
{
    private const string BaseUrl = "https://gitlab.example.com";
    private const string ProjectPath = "group%2Frepo";
    private const string CloneUrl = "https://gitlab.example.com/group/repo.git";
    private const string Token = "glpat-test";
    private const string DefaultBranch = "main";
    private const string MrUrl = "https://gitlab.example.com/group/repo/-/merge_requests/3";

    [Fact]
    public async Task CreatePullRequest_TargetGiven_OpensAgainstIt()
    {
        var handler = new CapturingHandler(_ => Json(HttpStatusCode.Created, $$"""{"web_url":"{{MrUrl}}"}"""));
        var sut = CreateSut(handler);

        await sut.CreatePullRequestAsync(
            Repo("agent-smith/19107"), "t", "d", CancellationToken.None,
            targetBranch: new BranchName("agent-smith/19100"));

        handler.LastBody.Should().Contain("\"target_branch\":\"agent-smith/19100\"");
    }

    [Fact]
    public async Task CreatePullRequest_TargetNull_OpensAgainstTheDefaultBranch()
    {
        var handler = new CapturingHandler(_ => Json(HttpStatusCode.Created, $$"""{"web_url":"{{MrUrl}}"}"""));
        var sut = CreateSut(handler);

        await sut.CreatePullRequestAsync(Repo("agent-smith/19107"), "t", "d", CancellationToken.None);

        handler.LastBody.Should().Contain($"\"target_branch\":\"{DefaultBranch}\"");
    }

    [Fact]
    public async Task ReadPullRequestBase_ReturnsTheBranchTheMergeRequestTargets()
    {
        var handler = new CapturingHandler(_ =>
            Json(HttpStatusCode.OK, """{"target_branch":"agent-smith/19100"}"""));
        var sut = CreateSut(handler);

        var current = await sut.ReadPullRequestBaseAsync(MrUrl, CancellationToken.None);

        current.Should().Be("agent-smith/19100");
    }

    [Fact]
    public async Task RetargetPullRequest_PutsTheNewTargetBranch()
    {
        var handler = new CapturingHandler(_ => Json(HttpStatusCode.OK, "{}"));
        var sut = CreateSut(handler);

        var moved = await sut.RetargetPullRequestAsync(
            MrUrl, new BranchName("agent-smith/19100"), CancellationToken.None);

        moved.Should().BeTrue();
        handler.LastMethod.Should().Be(HttpMethod.Put);
        handler.LastBody.Should().Contain("\"target_branch\":\"agent-smith/19100\"");
    }

    [Fact]
    public async Task RetargetPullRequest_PlatformRefuses_IsReportedNotThrown()
    {
        var handler = new CapturingHandler(_ => Json(HttpStatusCode.Forbidden, "{}"));
        var sut = CreateSut(handler);

        var moved = await sut.RetargetPullRequestAsync(
            MrUrl, new BranchName("agent-smith/19100"), CancellationToken.None);

        moved.Should().BeFalse("a run that has done its work is not failed by a refused move");
    }

    private static Repository Repo(string branch) => new(new BranchName(branch), CloneUrl);

    private static GitLabSourceProvider CreateSut(HttpMessageHandler handler) =>
        new(new GitLabSourceConnection(BaseUrl, ProjectPath, CloneUrl, Token, DefaultBranch),
            new HttpClient(handler), NullLogger<GitLabSourceProvider>.Instance);

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class CapturingHandler(Func<Uri, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public string? LastBody { get; private set; }

        public HttpMethod? LastMethod { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            if (request.Content is not null)
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request.RequestUri!);
        }
    }
}
