using System.Net;
using System.Text;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Octokit;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// p0500: the configured-branch-wins inversion was in ALL THREE remote providers, not
/// only the Azure one the operator hit — checked, not assumed. These prove GitHub and
/// GitLab now resolve through the same precedence, at the level where it is
/// observable: the ref each provider actually reads a file on.
/// </summary>
public sealed class RemoteProviderDefaultBranchTests
{
    private const string Token = "token";

    [Fact]
    public async Task GitLabSourceProvider_RepositoryDefaultBranch_WinsOverTheConfiguredValue()
    {
        var refs = new List<string>();
        var sut = GitLab(refs, projectJson: """{"default_branch":"main"}""");

        await sut.TryReadFileAsync(".agentsmith/contexts/api/context.yaml", CancellationToken.None);

        refs.Should().ContainSingle().Which.Should().Be(
            "main", "the project's own default branch wins over the configured 'develop'");
    }

    [Fact]
    public async Task GitLabSourceProvider_ProjectAnswersNothing_FallsBackToTheConfiguredValue()
    {
        var refs = new List<string>();
        var sut = GitLab(refs, projectJson: "{}");

        await sut.TryReadFileAsync(".agentsmith/contexts/api/context.yaml", CancellationToken.None);

        refs.Should().ContainSingle().Which.Should().Be("develop");
    }

    [Fact]
    public async Task GitHubSourceProvider_RepositoryDefaultBranch_WinsOverTheConfiguredValue()
    {
        var refs = new List<string>();
        var sut = GitHub(refs, repositoryDefaultBranch: "main");

        await sut.TryReadFileAsync(".agentsmith/contexts/api/context.yaml", CancellationToken.None);

        refs.Should().ContainSingle().Which.Should().Be("main");
    }

    [Fact]
    public async Task GitHubSourceProvider_RepositoryLookupFails_FallsBackToTheConfiguredValue()
    {
        var refs = new List<string>();
        var sut = GitHub(refs, repositoryDefaultBranch: null);

        await sut.TryReadFileAsync(".agentsmith/contexts/api/context.yaml", CancellationToken.None);

        refs.Should().ContainSingle().Which.Should().Be("develop");
    }

    private static GitHubSourceProvider GitHub(List<string> refs, string? repositoryDefaultBranch)
    {
        var contentsMock = new Mock<IRepositoryContentsClient>();
        contentsMock.Setup(c => c.GetAllContentsByRef(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string, string>((_, _, _, reference) => refs.Add(reference))
            .ThrowsAsync(new NotFoundException("Not Found", HttpStatusCode.NotFound));

        var repoMock = new Mock<IRepositoriesClient>();
        repoMock.SetupGet(r => r.Content).Returns(contentsMock.Object);
        var lookup = repoMock.Setup(r => r.Get(It.IsAny<string>(), It.IsAny<string>()));
        if (repositoryDefaultBranch is null)
            lookup.ThrowsAsync(new NotFoundException("Not Found", HttpStatusCode.NotFound));
        else
            lookup.ReturnsAsync(RepositoryWithDefaultBranch(repositoryDefaultBranch));

        var clientMock = new Mock<IGitHubClient>();
        clientMock.SetupGet(c => c.Repository).Returns(repoMock.Object);
        var factoryMock = new Mock<IGitHubClientFactory>();
        factoryMock.Setup(f => f.Create(It.IsAny<string>())).Returns(clientMock.Object);

        return new GitHubSourceProvider(
            new GitHubSourceConnection("https://github.com/example/repo", Token, DefaultBranch: "develop"),
            factoryMock.Object, NullLogger<GitHubSourceProvider>.Instance);
    }

    /// <summary>
    /// Octokit's Repository has no settable DefaultBranch, so the value is placed
    /// through the only door the type offers — its full constructor.
    /// </summary>
    private static Repository RepositoryWithDefaultBranch(string defaultBranch) =>
        new(url: "", htmlUrl: "", cloneUrl: "", gitUrl: "", sshUrl: "", svnUrl: "",
            mirrorUrl: "", archiveUrl: "", id: 1, nodeId: "", owner: null!, name: "repo",
            fullName: "example/repo", isTemplate: false, description: "", homepage: "",
            language: "", @private: false, fork: false, forksCount: 0, stargazersCount: 0,
            defaultBranch: defaultBranch, openIssuesCount: 0, pushedAt: null,
            createdAt: default, updatedAt: default, permissions: null!, parent: null!,
            source: null!, license: null!, hasIssues: false, hasWiki: false,
            hasDownloads: false, hasPages: false, subscribersCount: 0, size: 0,
            allowRebaseMerge: null, allowSquashMerge: null, allowMergeCommit: null,
            archived: false, watchersCount: 0, deleteBranchOnMerge: null,
            visibility: RepositoryVisibility.Public,
            hasDiscussions: false, topics: [], allowAutoMerge: null,
            allowUpdateBranch: false, webCommitSignoffRequired: null, securityAndAnalysis: null!);

    /// <summary>
    /// Answers the project lookup with <paramref name="projectJson"/> and records the
    /// <c>ref=</c> of every raw-file read, which is the branch the provider resolved.
    /// </summary>
    private static GitLabSourceProvider GitLab(List<string> refs, string projectJson)
    {
        var handler = new FakeHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/repository/files/", StringComparison.Ordinal))
            {
                var query = System.Web.HttpUtility.ParseQueryString(req.RequestUri.Query);
                refs.Add(query["ref"] ?? string.Empty);
                return Json("content");
            }
            return Json(projectJson);
        });

        return new GitLabSourceProvider(
            new GitLabSourceConnection(
                "https://gitlab.example.com", "group%2Frepo",
                "https://gitlab.example.com/group/repo.git", Token, DefaultBranch: "develop"),
            new HttpClient(handler),
            NullLogger<GitLabSourceProvider>.Instance);
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
