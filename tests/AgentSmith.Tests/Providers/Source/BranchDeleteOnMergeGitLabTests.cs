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
/// 2026-11-17-713eb: a GitLab merge request from an agent-smith/ branch deletes it when it merges —
/// asked for when the request is opened (so a person's merge deletes it too), when it leaves draft,
/// and on the merge the run performs.
/// </summary>
public sealed class BranchDeleteOnMergeGitLabTests
{
    private const string MrUrl = "https://gitlab.example.com/group/repo/-/merge_requests/7";

    [Fact]
    public async Task GitLab_Merge_AsksToRemoveTheSourceBranch()
    {
        var handler = new BodyHandler("""{"iid":7,"state":"merged"}""");

        await Sut(handler).CompletePullRequestAsync(MrUrl, new BranchName("agent-smith/init"), CancellationToken.None);

        handler.Bodies.Should().ContainSingle().Which.Should().Contain("\"should_remove_source_branch\":true");
    }

    [Fact]
    public async Task GitLab_MarkReady_AsksToRemoveTheSourceBranch()
    {
        var handler = new BodyHandler("""{"iid":7}""");

        await Sut(handler).MarkPullRequestReadyAsync(MrUrl, CancellationToken.None);

        handler.Bodies.Should().ContainSingle().Which.Should().Contain("\"remove_source_branch\":true");
    }

    [Theory]
    [InlineData("agent-smith/42", true)]
    [InlineData("feature/x", false)]
    public async Task GitLab_Create_RemovesTheSourceBranchOnMerge_OnlyForAnAgentSmithBranch(string branch, bool removes)
    {
        var handler = new BodyHandler("""{"web_url":"https://gitlab.example.com/group/repo/-/merge_requests/1"}""", HttpStatusCode.Created);

        await Sut(handler).CreatePullRequestAsync(
            new Repository(new BranchName(branch), "https://gitlab.example.com/group/repo.git"), "t", "d",
            CancellationToken.None, targetBranch: new BranchName("main"));

        handler.Bodies.Should().ContainSingle().Which.Should().Contain($"\"remove_source_branch\":{(removes ? "true" : "false")}");
    }

    private static GitLabSourceProvider Sut(HttpMessageHandler handler) =>
        new(new GitLabSourceConnection("https://gitlab.example.com", "group%2Frepo",
                "https://gitlab.example.com/group/repo.git", "glpat-test", "main"),
            new HttpClient(handler), NullLogger<GitLabSourceProvider>.Instance);

    private sealed class BodyHandler(string reply, HttpStatusCode code = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct));
            return new HttpResponseMessage(code) { Content = new StringContent(reply, Encoding.UTF8, "application/json") };
        }
    }
}
