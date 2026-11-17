using System.Net;
using System.Text;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// 2026-11-17-023fa: a code run's merge of a GitLab merge request. Bypass merges now; GitLab's refusal is reported
/// with the policy, the role to grant and the key that waits instead. Respect asks to merge when the pipeline
/// succeeds and reads the answer as armed.
/// </summary>
public sealed class GitLabMergeModeTests
{
    private const string MrUrl = "https://gitlab.example.com/group/repo/-/merge_requests/7";
    private static readonly BranchName Branch = new("agent-smith/42");

    [Fact]
    public async Task Bypass_MergesNow()
    {
        string? body = null;
        var sut = Sut(req => { body = req; return Json(HttpStatusCode.OK, """{"iid":7,"state":"merged"}"""); });

        var completion = await sut.MergePullRequestAsync(MrUrl, Branch, BranchProtection.Bypass, CancellationToken.None);

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Merged);
        body.Should().NotContain("merge_when_pipeline_succeeds");
    }

    [Fact]
    public async Task Bypass_Refused_NamesPolicyPermissionAndKey()
    {
        var sut = Sut(_ => Json(HttpStatusCode.MethodNotAllowed, """{"message":"Branch cannot be merged: approvals are missing"}"""));

        var completion = await sut.MergePullRequestAsync(MrUrl, Branch, BranchProtection.Bypass, CancellationToken.None);

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Refused);
        completion.Reason.Should().StartWith("cannot bypass the merge checks");
        completion.Reason.Should().Contain("on GitLab: GitLab answered 405: Branch cannot be merged: approvals are missing");
        completion.Reason.Should().EndWith("or set respect_branch_protection: true");
    }

    [Fact]
    public async Task Respect_AsksToMergeWhenThePipelineSucceeds_AndIsArmed()
    {
        string? body = null;
        var sut = Sut(req =>
        {
            body = req;
            return Json(HttpStatusCode.OK, """{"iid":7,"state":"opened","merge_when_pipeline_succeeds":true}""");
        });

        var completion = await sut.MergePullRequestAsync(MrUrl, Branch, BranchProtection.Respect, CancellationToken.None);

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Armed);
        body.Should().Contain("\"merge_when_pipeline_succeeds\":true");
    }

    [Fact]
    public void Respect_OpenedWithoutArming_IsRefused() =>
        GitLabMergeModes.Interpret(7, 200, true, """{"iid":7,"state":"opened"}""")
            .Outcome.Should().Be(PullRequestCompletionOutcome.Refused);

    [Fact]
    public void Respect_MergedAtOnce_IsMerged() =>
        GitLabMergeModes.Interpret(7, 200, true, """{"iid":7,"state":"merged"}""")
            .Outcome.Should().Be(PullRequestCompletionOutcome.Merged);

    private static GitLabSourceProvider Sut(Func<string, HttpResponseMessage> respond) =>
        new(new GitLabSourceConnection("https://gitlab.example.com", "group%2Frepo",
                "https://gitlab.example.com/group/repo.git", "glpat-test", "main"),
            new HttpClient(new Handler(respond)), NullLogger<GitLabSourceProvider>.Instance);

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            respond(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct));
    }
}
