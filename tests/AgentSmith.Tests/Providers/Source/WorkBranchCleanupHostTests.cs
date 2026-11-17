using System.Net;
using System.Text;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Moq;
using Octokit;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>2026-11-17-713ec: what each host answers a branch-cleanup run — its agent-smith/ branches (a name that
/// only starts with the prefix is not one), the pull requests from and into a branch, and the delete.</summary>
public sealed class WorkBranchCleanupHostTests
{
    [Fact]
    public async Task GitHub_ListsOnlyAgentSmithBranches_ReadsPullRequests_AndDeletes()
    {
        var refs = new Mock<IReferencesClient>();
        refs.Setup(r => r.GetAllForSubNamespace("o", "r", "heads/agent-smith/")).ReturnsAsync(
            [Ref("refs/heads/agent-smith/42", "s42"), Ref("refs/heads/agent-smithy/x", "sx")]);
        var prs = new Mock<IPullRequestsClient>();
        prs.Setup(p => p.GetAllForRepository("o", "r", It.Is<PullRequestRequest>(q => q.Head == "o:agent-smith/42")))
            .ReturnsAsync([Pr(ItemState.Closed, merged: true, "s42")]);
        prs.Setup(p => p.GetAllForRepository("o", "r", It.Is<PullRequestRequest>(q => q.Base == "agent-smith/42")))
            .ReturnsAsync([]);
        var host = new GitHubWorkBranches("o", "r", () => Client(refs.Object, prs.Object));

        var tips = await host.ListWorkBranchesAsync(CancellationToken.None);
        var of = await host.PullRequestsOfAsync("agent-smith/42", CancellationToken.None);
        await host.DeleteBranchAsync(tips.Single(), CancellationToken.None);

        tips.Should().ContainSingle().Which.Should().Be(new WorkBranchTip("agent-smith/42", "s42"));
        of.From.Should().ContainSingle().Which.Should().Be(new BranchPullRequest(BranchPullRequestState.Merged, "s42"));
        of.OpenInto.Should().Be(0);
        refs.Verify(r => r.Delete("o", "r", "heads/agent-smith/42"), Times.Once);
    }

    [Fact]
    public async Task GitHub_NoAgentSmithRef_ListsNone()
    {
        var refs = new Mock<IReferencesClient>();
        refs.Setup(r => r.GetAllForSubNamespace("o", "r", It.IsAny<string>()))
            .ThrowsAsync(new NotFoundException("Not Found", HttpStatusCode.NotFound));
        var host = new GitHubWorkBranches("o", "r", () => Client(refs.Object, Mock.Of<IPullRequestsClient>()));

        (await host.ListWorkBranchesAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task AzureRepos_DeletesWithTheTipAsOldId_AndMapsPullRequestStates()
    {
        var git = new Mock<GitHttpClient>(
            new Uri("https://localhost/fake"), new VssCredentials(new VssBasicCredential(string.Empty, "fake")));
        git.Setup(c => c.GetRefsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<bool?>(),
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GitRef { Name = "refs/heads/agent-smith/7", ObjectId = "s7" }, new GitRef { Name = "refs/heads/agent-smithy", ObjectId = "x" }]);
        git.Setup(c => c.GetPullRequestsAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.Is<GitPullRequestSearchCriteria>(q => q.SourceRefName == "refs/heads/agent-smith/7"),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GitPullRequest { Status = PullRequestStatus.Abandoned, LastMergeSourceCommit = new GitCommitRef { CommitId = "s7" } }]);
        git.Setup(c => c.GetPullRequestsAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.Is<GitPullRequestSearchCriteria>(q => q.TargetRefName == "refs/heads/agent-smith/7"),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GitPullRequest { Status = PullRequestStatus.Active }]);
        IEnumerable<GitRefUpdate>? sent = null;
        git.Setup(c => c.UpdateRefsAsync(It.IsAny<IEnumerable<GitRefUpdate>>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<GitRefUpdate>, string, string, string, object, CancellationToken>((u, _, _, _, _, _) => sent = u)
            .ReturnsAsync([new GitRefUpdateResult { Success = true }]);
        var host = new AzureReposWorkBranches("demo", "repo", _ => Task.FromResult(git.Object));

        var tip = (await host.ListWorkBranchesAsync(CancellationToken.None)).Should().ContainSingle().Subject;
        var of = await host.PullRequestsOfAsync(tip.Name, CancellationToken.None);
        var deleted = await host.DeleteBranchAsync(tip, CancellationToken.None);

        of.From.Should().ContainSingle().Which.State.Should().Be(BranchPullRequestState.Closed);
        of.OpenInto.Should().Be(1);
        deleted.Should().BeTrue();
        var update = sent!.Should().ContainSingle().Subject;
        update.OldObjectId.Should().Be("s7", "Azure refuses to delete a branch that moved since it was listed");
        update.NewObjectId.Should().Be(new string('0', 40));
    }

    [Fact]
    public async Task GitLab_ListsReadsAndDeletes()
    {
        var handler = new PathHandler(path => path switch
        {
            _ when path.Contains("/repository/branches?") =>
                """[{"name":"agent-smith/5","commit":{"id":"s5"}},{"name":"agent-smithy","commit":{"id":"x"}}]""",
            _ when path.Contains("source_branch=") => """[{"state":"merged","sha":"s5"}]""",
            _ when path.Contains("target_branch=") => "[]",
            _ => "{}",
        });
        var host = new GitLabWorkBranches("https://gitlab.example.com", "group%2Frepo", "t", new HttpClient(handler));

        var tip = (await host.ListWorkBranchesAsync(CancellationToken.None)).Should().ContainSingle().Subject;
        var of = await host.PullRequestsOfAsync(tip.Name, CancellationToken.None);
        var deleted = await host.DeleteBranchAsync(tip, CancellationToken.None);

        tip.Should().Be(new WorkBranchTip("agent-smith/5", "s5"));
        of.From.Should().ContainSingle().Which.Should().Be(new BranchPullRequest(BranchPullRequestState.Merged, "s5"));
        deleted.Should().BeTrue();
        handler.Seen.Should().Contain(s => s.StartsWith("DELETE ") && s.EndsWith("/repository/branches/agent-smith%2F5"));
    }

    private static IGitHubClient Client(IReferencesClient refs, IPullRequestsClient prs)
    {
        var client = new Mock<IGitHubClient>();
        var gitDb = new Mock<IGitDatabaseClient>();
        gitDb.SetupGet(g => g.Reference).Returns(refs);
        client.SetupGet(c => c.Git).Returns(gitDb.Object);
        client.SetupGet(c => c.PullRequest).Returns(prs);
        return client.Object;
    }

    // Octokit's models keep their setters non-public; a test sets them the way the deserializer does.
    private static PullRequest Pr(ItemState state, bool merged, string headSha)
    {
        var head = Set(new GitReference(), nameof(GitReference.Sha), headSha);
        var pr = Set(new PullRequest(1), nameof(PullRequest.State), new StringEnum<ItemState>(state));
        if (merged) Set(pr, nameof(PullRequest.MergedAt), (DateTimeOffset?)DateTimeOffset.UnixEpoch);
        return Set(pr, nameof(PullRequest.Head), head);
    }

    private static Reference Ref(string name, string sha) =>
        Set(Set(new Reference(), nameof(Reference.Ref), name), nameof(Reference.Object), Set(new TagObject(), nameof(TagObject.Sha), sha));

    private static T Set<T>(T target, string property, object value)
    {
        typeof(T).GetProperty(property)!.SetValue(target, value);
        return target;
    }

    private sealed class PathHandler(Func<string, string> reply) : HttpMessageHandler
    {
        public List<string> Seen { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.PathAndQuery;
            Seen.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(reply(path), Encoding.UTF8, "application/json") });
        }
    }
}
