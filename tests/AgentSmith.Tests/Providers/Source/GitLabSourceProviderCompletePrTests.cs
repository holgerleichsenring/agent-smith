using System.Net;
using System.Text;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// p0490: GitLab finishes an init merge request by merging it — PUT
/// /merge_requests/:iid/merge. A merge GitLab will not perform answers 405 with the
/// sentence naming what stopped it (approvals, pipeline, protected branch); that
/// sentence is the reason recorded for the repo, and the merge request stays open.
/// </summary>
public sealed class GitLabSourceProviderCompletePrTests
{
    private const string BaseUrl = "https://gitlab.example.com";
    private const string ProjectPath = "group%2Frepo";
    private const string CloneUrl = "https://gitlab.example.com/group/repo.git";
    private const string MrUrl = "https://gitlab.example.com/group/repo/-/merge_requests/7";
    private const string Token = "glpat-test";

    [Fact]
    public async Task GitLabSourceProvider_CompletePullRequest_MergesIt()
    {
        var seen = new List<(HttpMethod Method, string Path)>();
        var handler = new RecordingHandler(seen, (_, _) =>
            Json(HttpStatusCode.OK, """{"iid":7,"state":"merged"}"""));

        var completion = await CreateSut(handler).CompletePullRequestAsync(
            MrUrl, new BranchName("agentsmith/init"), CancellationToken.None);

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Merged);
        seen.Should().ContainSingle();
        seen[0].Method.Should().Be(HttpMethod.Put);
        seen[0].Path.Should().EndWith($"/projects/{ProjectPath}/merge_requests/7/merge");
    }

    [Fact]
    public async Task GitLabSourceProvider_CompletePullRequest_PolicyRefuses_ReportsGitLabsMessage()
    {
        var handler = new RecordingHandler([], (_, _) => Json(
            HttpStatusCode.MethodNotAllowed,
            """{"message":"Branch cannot be merged: approvals are missing"}"""));

        var completion = await CreateSut(handler).CompletePullRequestAsync(
            MrUrl, new BranchName("agentsmith/init"), CancellationToken.None);

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Refused);
        completion.Reason.Should().Contain("approvals are missing");
        completion.Reason.Should().Contain("405");
    }

    [Fact]
    public async Task GitLabSourceProvider_CompletePullRequest_LeftUnmerged_IsRefused()
    {
        // A 200 whose state is not "merged" means GitLab accepted the call and did not
        // merge (merge-when-pipeline-succeeds); the merge request is still open.
        var handler = new RecordingHandler([], (_, _) =>
            Json(HttpStatusCode.OK, """{"iid":7,"state":"opened"}"""));

        var completion = await CreateSut(handler).CompletePullRequestAsync(
            MrUrl, new BranchName("agentsmith/init"), CancellationToken.None);

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Refused);
        completion.Reason.Should().Contain("opened");
    }

    private static GitLabSourceProvider CreateSut(HttpMessageHandler handler) =>
        new(new GitLabSourceConnection(BaseUrl, ProjectPath, CloneUrl, Token, "main"),
            new HttpClient(handler), NullLogger<GitLabSourceProvider>.Instance);

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class RecordingHandler(
        List<(HttpMethod Method, string Path)> seen,
        Func<HttpMethod, Uri, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            seen.Add((request.Method, request.RequestUri!.AbsolutePath));
            return Task.FromResult(respond(request.Method, request.RequestUri!));
        }
    }
}
